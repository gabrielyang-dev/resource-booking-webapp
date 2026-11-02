using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Booking.Api.Rules;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Features.AddOns;

// The add-on catalogue: what can be ordered with a booking, at which location.
public class AddOnsManager
{
    private readonly AppDbContext _db;

    public AddOnsManager(AppDbContext db)
    {
        _db = db;
    }

    // Members see active add-ons only; admins can ask for inactive ones too.
    public Task<PagedResponse<AddOnResponse>> ListAsync(Guid? locationId, bool includeInactive, int page, int pageSize)
    {
        var query = _db.AddOns.AsNoTracking().Include(a => a.Location)
            .Where(a => locationId == null || a.LocationId == locationId)
            .Where(a => includeInactive || a.IsActive)
            .OrderBy(a => a.Name).ThenBy(a => a.Id);
        return Paging.ToPageAsync(query, page, pageSize, AddOnResponse.From);
    }

    public async Task<AddOnResponse> CreateAsync(AddOnRequest request)
    {
        var addOn = new AddOn { Id = Guid.NewGuid() };
        await ApplyAsync(addOn, request);
        _db.AddOns.Add(addOn);
        await SaveAsync();
        return await GetAsync(addOn.Id);
    }

    public async Task<AddOnResponse> UpdateAsync(Guid id, AddOnRequest request)
    {
        var addOn = await _db.AddOns.FirstOrDefaultAsync(a => a.Id == id)
            ?? throw ApiException.NotFound("There is no add-on with that id. Check the id and try again.");
        await ApplyAsync(addOn, request);
        await SaveAsync();
        return await GetAsync(id);
    }

    private async Task<AddOnResponse> GetAsync(Guid id) =>
        AddOnResponse.From(await _db.AddOns.AsNoTracking().Include(a => a.Location).FirstAsync(a => a.Id == id));

    // Validates every field at once, then copies them onto the add-on.
    private async Task ApplyAsync(AddOn addOn, AddOnRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > 200)
        {
            errors["name"] = ["Enter a name of up to 200 characters, for example Sandwiches."];
        }
        if (request.LocationId is null || !await _db.Locations.AnyAsync(l => l.Id == request.LocationId))
        {
            errors["locationId"] = ["Choose a location from the list."];
        }
        if (request.LeadTimeMinutes is null or < 0 or > 60 * 24 * 30)
        {
            errors["leadTimeMinutes"] = ["Enter the notice needed in minutes, from 0 to 43,200 (30 days)."];
        }
        var fromOk = RuleSettings.TryParseTime(request.AvailableFrom, out var from);
        var untilOk = RuleSettings.TryParseTime(request.AvailableUntil, out var until);
        if (!fromOk || !untilOk || until <= from)
        {
            errors["availableFrom"] = ["Enter available hours like 08:00 to 15:00, with the end after the start."];
        }
        if (request.FulfillerId is not null && !await _db.Users.AnyAsync(u => u.Id == request.FulfillerId))
        {
            errors["fulfillerId"] = ["Choose the person who delivers it from the list of users, or leave it empty."];
        }
        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }

        addOn.LocationId = request.LocationId!.Value;
        addOn.Name = name;
        addOn.LeadTimeMinutes = request.LeadTimeMinutes!.Value;
        addOn.AvailableFrom = from;
        addOn.AvailableUntil = until;
        addOn.FulfillerId = request.FulfillerId;
        addOn.IsActive = request.Active ?? true;
    }

    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ApiException.Conflict("NAME_TAKEN", "That location already has an add-on with this name. Choose another name.");
        }
    }
}
