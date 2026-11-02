using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Features.Rules;
using Booking.Api.Infrastructure;
using Booking.Api.Rules;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.Bookings;

// Answers "would this booking be accepted?" without writing anything: the rules, plus a free slot.
// BookingManager uses it before writing; it also finds alternatives and free resources.
public class BookingJudge
{
    public const int MaxAlternatives = 3;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    public static readonly BookingStatus[] HoldingStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly RuleEngine _engine;
    private readonly RulesManager _rules;

    public BookingJudge(AppDbContext db, IClock clock, RuleEngine engine, RulesManager rules)
    {
        _db = db;
        _clock = clock;
        _engine = engine;
        _rules = rules;
    }

    // Every check and every effective rule for the resource, judged for the owner. Empty when allowed.
    public async Task<List<Violation>> CheckRulesAsync(Resource resource, Guid ownerId, TimeRange range, int? attendees, Guid? excludeBookingId)
    {
        var settings = await _rules.GetEffectiveAsync(resource.Id, resource.ResourceTypeId);
        var others = await LoadOwnerBookingsAsync(ownerId, resource.ResourceTypeId, range, excludeBookingId);
        return _engine.Evaluate(Candidate(resource, ownerId, range, attendees, others), settings);
    }

    // Up to 3 valid times on the same resource (same length, same local day, nearest first)
    // and up to 3 other resources of the same type and location that are free at the requested time.
    public async Task<List<AlternativeResponse>> FindAlternativesAsync(
        Resource resource, Guid ownerId, TimeRange range, int? attendees, Guid? excludeBookingId)
    {
        var alternatives = new List<AlternativeResponse>();
        if (range.End <= range.Start)
        {
            return alternatives;
        }

        var zone = LocalTime.Zone(resource.Location.TimeZone);
        var day = LocalTime.Day(DateOnly.FromDateTime(LocalTime.ToLocal(range.Start, zone)), zone);
        var others = await LoadOwnerBookingsAsync(ownerId, resource.ResourceTypeId, day, excludeBookingId);

        // Same resource, other times.
        var settings = await _rules.GetEffectiveAsync(resource.Id, resource.ResourceTypeId);
        var taken = await LoadHoldingAsync([resource.Id], day, excludeBookingId);
        var times = AlternativeTimes.Find(range, day, Step, MaxAlternatives, candidate =>
            !taken.Any(t => t.Range.Overlaps(candidate))
            && _engine.Evaluate(Candidate(resource, ownerId, candidate, attendees, others), settings).Count == 0);
        alternatives.AddRange(times.Select(t => new AlternativeResponse(resource.Id, resource.Name, t.Start, t.End)));

        // Other resources, same time.
        var siblings = await _db.Resources.AsNoTracking().Include(r => r.Location)
            .Where(r => r.IsActive && r.Id != resource.Id
                && r.ResourceTypeId == resource.ResourceTypeId && r.LocationId == resource.LocationId)
            .OrderBy(r => r.Name).ThenBy(r => r.Id)
            .ToListAsync();
        alternatives.AddRange((await FilterFreeAsync(siblings, ownerId, range, attendees, others, MaxAlternatives))
            .Select(r => new AlternativeResponse(r.Id, r.Name, range.Start, range.End)));
        return alternatives;
    }

    // Active resources (optionally of one location and type) that are free and pass every rule for the time.
    public async Task<List<Resource>> FindAvailableAsync(Guid? locationId, Guid? resourceTypeId, Guid ownerId, TimeRange range, int? attendees)
    {
        var resources = await _db.Resources.AsNoTracking().Include(r => r.Location).Include(r => r.ResourceType)
            .Where(r => r.IsActive)
            .Where(r => locationId == null || r.LocationId == locationId)
            .Where(r => resourceTypeId == null || r.ResourceTypeId == resourceTypeId)
            .OrderBy(r => r.Name).ThenBy(r => r.Id)
            .ToListAsync();
        var free = new List<Resource>();
        foreach (var group in resources.GroupBy(r => r.ResourceTypeId))
        {
            var others = await LoadOwnerBookingsAsync(ownerId, group.Key, range, null);
            free.AddRange(await FilterFreeAsync(group.ToList(), ownerId, range, attendees, others, int.MaxValue));
        }
        return free.OrderBy(r => r.Name).ThenBy(r => r.Id).ToList();
    }

    private async Task<List<Resource>> FilterFreeAsync(
        List<Resource> resources, Guid ownerId, TimeRange range, int? attendees, List<TimeRange> ownerOthers, int max)
    {
        var taken = await LoadHoldingAsync(resources.Select(r => r.Id).ToList(), range, null);
        var free = new List<Resource>();
        foreach (var resource in resources)
        {
            if (free.Count == max)
            {
                break;
            }
            if (taken.Any(t => t.ResourceId == resource.Id))
            {
                continue;
            }
            var settings = await _rules.GetEffectiveAsync(resource.Id, resource.ResourceTypeId);
            if (_engine.Evaluate(Candidate(resource, ownerId, range, attendees, ownerOthers), settings).Count == 0)
            {
                free.Add(resource);
            }
        }
        return free;
    }

    // Slot-holding bookings on these resources that overlap the range.
    private async Task<List<(Guid ResourceId, TimeRange Range)>> LoadHoldingAsync(List<Guid> resourceIds, TimeRange range, Guid? excludeBookingId)
    {
        var rows = await _db.Bookings.AsNoTracking()
            .Where(b => resourceIds.Contains(b.ResourceId) && HoldingStatuses.Contains(b.Status)
                && b.Start < range.End && range.Start < b.End && b.Id != excludeBookingId)
            .Select(b => new { b.ResourceId, b.Start, b.End })
            .ToListAsync();
        return rows.Select(b => (b.ResourceId, new TimeRange(b.Start, b.End))).ToList();
    }

    // The owner's other slot-holding bookings on this type of resource around the range, for the daily quota.
    private async Task<List<TimeRange>> LoadOwnerBookingsAsync(Guid ownerId, Guid resourceTypeId, TimeRange range, Guid? excludeBookingId)
    {
        var from = range.Start.AddDays(-1);
        var to = range.End.AddDays(1);
        return await _db.Bookings.AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.Resource.ResourceTypeId == resourceTypeId
                && HoldingStatuses.Contains(b.Status) && b.Start < to && from < b.End
                && b.Id != excludeBookingId)
            .Select(b => new TimeRange(b.Start, b.End))
            .ToListAsync();
    }

    private BookingCandidate Candidate(Resource resource, Guid ownerId, TimeRange range, int? attendees, List<TimeRange> ownerOthers) =>
        new(resource.Name, resource.Capacity, resource.Location.TimeZone, ownerId, range, attendees, _clock.UtcNow, ownerOthers);
}
