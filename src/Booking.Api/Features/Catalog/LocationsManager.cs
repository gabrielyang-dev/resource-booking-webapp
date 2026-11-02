using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Features.Catalog;

public class LocationsManager
{
    private readonly AppDbContext _db;

    public LocationsManager(AppDbContext db)
    {
        _db = db;
    }

    public Task<PagedResponse<LocationResponse>> ListAsync(int page, int pageSize)
    {
        var ordered = _db.Locations.AsNoTracking().OrderBy(l => l.Name).ThenBy(l => l.Id);
        return Paging.ToPageAsync(ordered, page, pageSize, LocationResponse.From);
    }

    public async Task<LocationResponse> GetAsync(Guid id)
    {
        var location = await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        return LocationResponse.From(location ?? throw LocationNotFound());
    }

    public async Task<LocationResponse> CreateAsync(LocationRequest request)
    {
        var (name, timeZone) = Validate(request);
        await EnsureNameIsFree(name, exceptId: null);

        var location = new Location { Id = Guid.NewGuid(), Name = name, TimeZone = timeZone };
        _db.Locations.Add(location);
        await SaveAsync();
        return LocationResponse.From(location);
    }

    public async Task<LocationResponse> UpdateAsync(Guid id, LocationRequest request)
    {
        var (name, timeZone) = Validate(request);
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id) ?? throw LocationNotFound();
        await EnsureNameIsFree(name, exceptId: id);

        location.Name = name;
        location.TimeZone = timeZone;
        await SaveAsync();
        return LocationResponse.From(location);
    }

    public async Task DeleteAsync(Guid id)
    {
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id) ?? throw LocationNotFound();
        var used = await _db.Resources.CountAsync(r => r.LocationId == id);
        if (used > 0)
        {
            throw InUse(location.Name, used);
        }

        _db.Locations.Remove(location);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // A resource was added after the count above; the foreign key still refuses the delete.
            throw InUse(location.Name, await _db.Resources.CountAsync(r => r.LocationId == id));
        }
    }

    private static ApiException LocationNotFound() =>
        ApiException.NotFound("There is no location with that id. Check the id and try again.");

    private static ApiException InUse(string name, int count) =>
        ApiException.Conflict("IN_USE",
            $"{name} is used by {count} resource{(count == 1 ? "" : "s")} (active or inactive). Move them to another location first, then delete this location.");

    private static ApiException NameTaken() =>
        ApiException.Conflict("NAME_TAKEN", "A location with that name already exists. Use a different name.");

    private static (string Name, string TimeZone) Validate(LocationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = CatalogValidation.CleanName(request.Name, errors);

        var timeZone = request.TimeZone?.Trim() ?? "";
        if (!TimeZoneIds.IsIana(timeZone))
        {
            errors["timeZone"] = ["Enter an IANA time zone, spelled exactly (the case matters), such as Europe/London or America/New_York. Windows names like \"GMT Standard Time\" are not accepted."];
        }

        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
        return (name, timeZone);
    }

    private async Task EnsureNameIsFree(string name, Guid? exceptId)
    {
        var taken = await _db.Locations.AnyAsync(l => l.Name.ToLower() == name.ToLower() && l.Id != exceptId);
        if (taken)
        {
            throw NameTaken();
        }
    }

    // The unique index is the final guard if two requests race past the check above.
    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Locations_Name" })
        {
            throw NameTaken();
        }
    }
}
