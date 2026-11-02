using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Rules;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Booking.Api.Rules;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Features.Bookings;

// The only class that writes bookings.
public class BookingManager
{
    private const int MaxAttendees = 10_000;
    private const int MaxKeyLength = 200;

    private static readonly BookingStatus[] HoldingStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly RuleEngine _engine;
    private readonly RulesManager _rules;

    public BookingManager(AppDbContext db, IClock clock, RuleEngine engine, RulesManager rules)
    {
        _db = db;
        _clock = clock;
        _engine = engine;
        _rules = rules;
    }

    // Admins may book for someone else (request.OwnerId); the rules are then judged for that person.
    public async Task<CreateBookingResult> CreateAsync(Guid currentUserId, bool isAdmin, BookingRequest request, string? idempotencyKey)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        var (resourceId, range) = Validate(request, key);
        var ownerId = await ResolveOwnerAsync(currentUserId, isAdmin, request.OwnerId);

        if (key is not null)
        {
            var existing = await FindByKeyAsync(ownerId, key);
            if (existing is not null)
            {
                return new CreateBookingResult(existing, Created: false);
            }
        }

        var resource = await LoadBookableResourceAsync(resourceId);
        await EnforceRulesAsync(resource, ownerId, range, request.Attendees, excludeBookingId: null);

        var clash = await FindClashAsync(resourceId, range, excludeBookingId: null);
        if (clash is not null)
        {
            // The clash may be this same request, sent twice, that committed after the key check above.
            var replay = key is null ? null : await FindByKeyAsync(ownerId, key);
            if (replay is not null)
            {
                return new CreateBookingResult(replay, Created: false);
            }
            throw SlotTaken(resource, clash);
        }

        var booking = new Domain.Booking
        {
            Id = Guid.NewGuid(),
            ResourceId = resourceId,
            OwnerId = ownerId,
            Start = range.Start,
            End = range.End,
            Status = BookingStatus.Confirmed,
            Attendees = request.Attendees,
            IdempotencyKey = key,
            CreatedAt = _clock.UtcNow,
        };
        _db.Bookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // Another request took the slot between our check and the insert; the database caught it.
            _db.ChangeTracker.Clear();
            // If the winner was the same request sent twice (same key), this is a replay, not a clash.
            var stored = key is null ? null : await FindByKeyAsync(ownerId, key);
            if (stored is not null)
            {
                return new CreateBookingResult(stored, Created: false);
            }
            var winner = await FindClashAsync(resourceId, range, excludeBookingId: null);
            throw winner is null ? SlotTakenWithoutTimes(resource) : SlotTaken(resource, winner);
        }
        catch (DbUpdateException ex) when (key is not null && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same key raced us and won: return its booking.
            _db.ChangeTracker.Clear();
            var stored = await FindByKeyAsync(ownerId, key);
            if (stored is not null)
            {
                return new CreateBookingResult(stored, Created: false);
            }
            throw;
        }

        return new CreateBookingResult(await GetAsync(booking.Id), Created: true);
    }

    public async Task<BookingResponse> GetForUserAsync(Guid id, Guid userId, bool isAdmin)
    {
        var booking = await WithDetails().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id)
            ?? throw BookingNotFound();
        if (booking.OwnerId != userId && !isAdmin)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "FORBIDDEN",
                "You can only open your own bookings. Ask an admin if you need this one.");
        }
        return BookingResponse.From(booking);
    }

    // Moves a booking (time, resource or attendees). The owner stays the owner, even when an admin moves it.
    public async Task<BookingResponse> RescheduleAsync(Guid id, Guid userId, bool isAdmin, RescheduleRequest request)
    {
        if (request.Version is null)
        {
            throw ApiException.Invalid(new Dictionary<string, string[]>
            {
                ["version"] = ["Send the version of the booking you opened, so we can tell if someone else changed it."],
            });
        }
        var (resourceId, range) = Validate(new BookingRequest(request.ResourceId, request.Start, request.End, request.Attendees), null);

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id) ?? throw BookingNotFound();
        EnsureCanChange(booking, userId, isAdmin);
        if (booking.Version != request.Version)
        {
            throw StaleVersion();
        }
        if (!HoldingStatuses.Contains(booking.Status))
        {
            throw ApiException.Conflict("INVALID_STATE",
                $"This booking is {booking.Status.ToString().ToLowerInvariant()} and can't be changed. Make a new booking instead.");
        }

        var resource = await LoadBookableResourceAsync(resourceId);
        // Every rule runs again, for the owner (not the admin who may be moving it), ignoring the booking itself.
        await EnforceRulesAsync(resource, booking.OwnerId, range, request.Attendees, excludeBookingId: id);
        var clash = await FindClashAsync(resourceId, range, excludeBookingId: id);
        if (clash is not null)
        {
            throw SlotTaken(resource, clash);
        }

        booking.ResourceId = resourceId;
        booking.Start = range.Start;
        booking.End = range.End;
        booking.Attendees = request.Attendees;
        // Version is a concurrency token: the UPDATE only matches if nobody changed the row since we read it.
        booking.Version++;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw StaleVersion();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            throw SlotTakenWithoutTimes(resource);
        }

        return await GetAsync(id);
    }

    // Compare-and-set: only a slot-holding booking becomes Cancelled, so cancelling twice is harmless.
    public async Task<BookingResponse> CancelAsync(Guid id, Guid userId, bool isAdmin)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id) ?? throw BookingNotFound();
        EnsureCanChange(booking, userId, isAdmin);

        var updated = await _db.Bookings
            .Where(b => b.Id == id && HoldingStatuses.Contains(b.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.Cancelled)
                .SetProperty(b => b.Version, b => b.Version + 1));

        if (updated == 0)
        {
            var current = await _db.Bookings.AsNoTracking().FirstAsync(b => b.Id == id);
            if (current.Status != BookingStatus.Cancelled)
            {
                throw ApiException.Conflict("INVALID_STATE",
                    $"This booking is {current.Status.ToString().ToLowerInvariant()} and can't be cancelled.");
            }
        }

        return await GetAsync(id);
    }

    private static ApiException BookingNotFound() =>
        ApiException.NotFound("There is no booking with that id. Check the id and try again.");

    public Task<PagedResponse<BookingResponse>> ListMineAsync(Guid userId, bool mine, int page, int pageSize)
    {
        if (!mine)
        {
            throw ApiException.Invalid(new Dictionary<string, string[]>
            {
                ["mine"] = ["Only your own bookings can be listed for now. Use mine=true."],
            });
        }
        var query = WithDetails().AsNoTracking()
            .Where(b => b.OwnerId == userId)
            .OrderBy(b => b.Start).ThenBy(b => b.Id);
        return Paging.ToPageAsync(query, page, pageSize, BookingResponse.From);
    }

    // Slot-holding bookings on the resource that overlap [from, to).
    public async Task<IReadOnlyList<ResourceBookingResponse>> ListForResourceAsync(
        Guid resourceId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null)
        {
            errors["from"] = ["Enter the start of the period, for example 2026-11-05T00:00:00Z."];
        }
        if (to is null)
        {
            errors["to"] = ["Enter the end of the period, for example 2026-11-06T00:00:00Z."];
        }
        if (from is not null && to is not null && to <= from)
        {
            errors["to"] = ["The end of the period must be after its start."];
        }
        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }

        if (!await _db.Resources.AnyAsync(r => r.Id == resourceId))
        {
            throw ApiException.NotFound("There is no resource with that id. Check the id and try again.");
        }

        var start = from!.Value.UtcDateTime;
        var end = to!.Value.UtcDateTime;
        var rows = await _db.Bookings.Include(b => b.Owner).AsNoTracking()
            .Where(b => b.ResourceId == resourceId && HoldingStatuses.Contains(b.Status) && b.Start < end && start < b.End)
            .OrderBy(b => b.Start).ThenBy(b => b.Id)
            .ToListAsync();
        return rows.Select(ResourceBookingResponse.From).ToList();
    }

    private IQueryable<Domain.Booking> WithDetails() =>
        _db.Bookings.Include(b => b.Resource).ThenInclude(r => r.Location).Include(b => b.Owner);

    private async Task<BookingResponse> GetAsync(Guid id)
    {
        var booking = await WithDetails().AsNoTracking().FirstAsync(b => b.Id == id);
        return BookingResponse.From(booking);
    }

    private async Task<BookingResponse?> FindByKeyAsync(Guid ownerId, string key)
    {
        var booking = await WithDetails().AsNoTracking()
            .FirstOrDefaultAsync(b => b.OwnerId == ownerId && b.IdempotencyKey == key);
        return booking is null ? null : BookingResponse.From(booking);
    }

    // The first slot-holding booking on the resource that overlaps the range (half-open).
    // A reschedule passes its own id so a booking never clashes with itself.
    private Task<Domain.Booking?> FindClashAsync(Guid resourceId, TimeRange range, Guid? excludeBookingId) =>
        _db.Bookings.AsNoTracking()
            .Where(b => b.ResourceId == resourceId && HoldingStatuses.Contains(b.Status)
                && b.Start < range.End && range.Start < b.End
                && b.Id != excludeBookingId)
            .OrderBy(b => b.Start).ThenBy(b => b.Id)
            .FirstOrDefaultAsync();

    private async Task<Guid> ResolveOwnerAsync(Guid currentUserId, bool isAdmin, Guid? requestedOwnerId)
    {
        if (!isAdmin || requestedOwnerId is null || requestedOwnerId == currentUserId)
        {
            return currentUserId;
        }
        if (!await _db.Users.AnyAsync(u => u.Id == requestedOwnerId))
        {
            throw ApiException.Invalid(new() { ["ownerId"] = ["That user does not exist. Choose a user from the list."] });
        }
        return requestedOwnerId.Value;
    }

    // Runs every check and the resource's effective rules; throws one 422 listing every problem.
    private async Task EnforceRulesAsync(Resource resource, Guid ownerId, TimeRange range, int? attendees, Guid? excludeBookingId)
    {
        // The owner's other bookings on this type of resource around the same day, for the daily quota.
        var from = range.Start.AddDays(-1);
        var to = range.End.AddDays(1);
        var others = await _db.Bookings.AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.Resource.ResourceTypeId == resource.ResourceTypeId
                && HoldingStatuses.Contains(b.Status) && b.Start < to && from < b.End
                && b.Id != excludeBookingId)
            .Select(b => new TimeRange(b.Start, b.End))
            .ToListAsync();

        var candidate = new BookingCandidate(resource.Name, resource.Capacity, resource.Location.TimeZone,
            ownerId, range, attendees, _clock.UtcNow, others);
        var settings = await _rules.GetEffectiveAsync(resource.Id, resource.ResourceTypeId);
        var violations = _engine.Evaluate(candidate, settings);
        if (violations.Count > 0)
        {
            throw ApiException.RuleViolation(violations);
        }
    }

    private async Task<Resource> LoadBookableResourceAsync(Guid resourceId)
    {
        var resource = await _db.Resources.Include(r => r.Location).AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resourceId);
        if (resource is null || !resource.IsActive)
        {
            throw ApiException.Invalid(new Dictionary<string, string[]>
            {
                ["resourceId"] = ["That resource does not exist or is not available. Choose a resource from the list."],
            });
        }
        return resource;
    }

    // Owners change their own bookings; admins can change anyone's.
    private static void EnsureCanChange(Domain.Booking booking, Guid userId, bool isAdmin)
    {
        if (booking.OwnerId != userId && !isAdmin)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "FORBIDDEN",
                "You can only change your own bookings. Ask an admin if this one needs changing.");
        }
    }

    private static ApiException StaleVersion() =>
        ApiException.Conflict("STALE_VERSION", "This booking changed since you opened it. Reload and try again.");

    // Returns the resource id and the UTC range, or throws one 400 listing every bad field.
    private static (Guid ResourceId, TimeRange Range) Validate(BookingRequest request, string? key)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ResourceId is null)
        {
            errors["resourceId"] = ["Choose a resource."];
        }
        if (request.Start is null)
        {
            errors["start"] = ["Enter a start time, for example 2026-11-05T10:00:00Z."];
        }
        if (request.End is null)
        {
            errors["end"] = ["Enter an end time, for example 2026-11-05T11:00:00Z."];
        }
        if (request.Attendees is < 1 or > MaxAttendees)
        {
            errors["attendees"] = [$"Attendees must be a whole number from 1 to {MaxAttendees:N0}, or left empty."];
        }
        if (key is not null && key.Length > MaxKeyLength)
        {
            errors["idempotencyKey"] = [$"The Idempotency-Key must be {MaxKeyLength} characters or fewer."];
        }
        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
        return (request.ResourceId!.Value, new TimeRange(request.Start!.Value.UtcDateTime, request.End!.Value.UtcDateTime));
    }

    private static ApiException SlotTaken(Resource resource, Domain.Booking clash)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(resource.Location.TimeZone);
        var start = TimeZoneInfo.ConvertTimeFromUtc(clash.Start, zone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(clash.End, zone);
        // Add the date only when the clash does not start and end on the same local day.
        var format = start.Date == end.Date ? "HH:mm" : "d MMM HH:mm";
        return ApiException.Conflict("SLOT_TAKEN",
            $"{resource.Name} is already booked from {start.ToString(format)} to {end.ToString(format)}. " +
            "Pick another time or another resource.");
    }

    private static ApiException SlotTakenWithoutTimes(Resource resource) =>
        ApiException.Conflict("SLOT_TAKEN",
            $"{resource.Name} is already booked at that time. Pick another time or another resource.");
}
