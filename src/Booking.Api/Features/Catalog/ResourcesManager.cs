using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Features.Catalog;

public class ResourcesManager
{
    private const int MaxCapacity = 10_000;

    private readonly AppDbContext _db;

    public ResourcesManager(AppDbContext db)
    {
        _db = db;
    }

    // Inactive resources are shown only when asked for (the controller allows that for admins only).
    // An unknown filter id gives an empty page, not an error.
    public Task<PagedResponse<ResourceResponse>> ListAsync(
        Guid? locationId, Guid? resourceTypeId, bool includeInactive, int page, int pageSize)
    {
        var query = ResourcesWithNames().AsNoTracking();
        if (locationId is not null)
        {
            query = query.Where(r => r.LocationId == locationId);
        }
        if (resourceTypeId is not null)
        {
            query = query.Where(r => r.ResourceTypeId == resourceTypeId);
        }
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        var ordered = query.OrderBy(r => r.Name).ThenBy(r => r.Id);
        return Paging.ToPageAsync(ordered, page, pageSize, ResourceResponse.From);
    }

    // A member asking for an inactive resource gets the same 404 as for one that does not exist.
    public async Task<ResourceResponse> GetAsync(Guid id, bool isAdmin)
    {
        var resource = await ResourcesWithNames().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (resource is null || (!resource.IsActive && !isAdmin))
        {
            throw ResourceNotFound();
        }
        return ResourceResponse.From(resource);
    }

    public async Task<ResourceResponse> CreateAsync(ResourceRequest request)
    {
        var (name, locationId, typeId) = await ValidateAsync(request, activeRequired: false);
        await EnsureNameIsFree(locationId, name, exceptId: null);

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            ResourceTypeId = typeId,
            Name = name,
            Capacity = request.Capacity,
            IsActive = request.Active ?? true,
        };
        _db.Resources.Add(resource);
        await SaveAsync();
        return await GetAsync(resource.Id, isAdmin: true);
    }

    public async Task<ResourceResponse> UpdateAsync(Guid id, ResourceRequest request)
    {
        var (name, locationId, typeId) = await ValidateAsync(request, activeRequired: true);
        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == id) ?? throw ResourceNotFound();
        await EnsureNameIsFree(locationId, name, exceptId: id);

        resource.LocationId = locationId;
        resource.ResourceTypeId = typeId;
        resource.Name = name;
        resource.Capacity = request.Capacity;
        resource.IsActive = request.Active!.Value;
        await SaveAsync();
        return await GetAsync(id, isAdmin: true);
    }

    private IQueryable<Resource> ResourcesWithNames() =>
        _db.Resources.Include(r => r.Location).Include(r => r.ResourceType);

    private static ApiException ResourceNotFound() =>
        ApiException.NotFound("There is no resource with that id. Check the id and try again.");

    private static ApiException NameTaken() =>
        ApiException.Conflict("NAME_TAKEN", "That location already has a resource with that name. Use a different name.");

    // Returns the cleaned values, or throws one 400 listing every bad field.
    private async Task<(string Name, Guid LocationId, Guid TypeId)> ValidateAsync(ResourceRequest request, bool activeRequired)
    {
        var errors = new Dictionary<string, string[]>();
        var name = CatalogValidation.CleanName(request.Name, errors);

        if (request.LocationId is null)
        {
            errors["locationId"] = ["Choose a location."];
        }
        else if (!await _db.Locations.AnyAsync(l => l.Id == request.LocationId))
        {
            errors["locationId"] = ["That location does not exist. Choose a location from the list."];
        }

        if (request.ResourceTypeId is null)
        {
            errors["resourceTypeId"] = ["Choose a resource type."];
        }
        else if (!await _db.ResourceTypes.AnyAsync(t => t.Id == request.ResourceTypeId))
        {
            errors["resourceTypeId"] = ["That resource type does not exist. Choose a resource type from the list."];
        }

        if (request.Capacity is < 1 or > MaxCapacity)
        {
            errors["capacity"] = [$"Capacity must be a whole number from 1 to {MaxCapacity:N0}, or left empty if it is not a limit on people."];
        }

        if (activeRequired && request.Active is null)
        {
            errors["active"] = ["Choose active or inactive."];
        }

        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
        return (name, request.LocationId!.Value, request.ResourceTypeId!.Value);
    }

    private async Task EnsureNameIsFree(Guid locationId, string name, Guid? exceptId)
    {
        var taken = await _db.Resources.AnyAsync(r =>
            r.LocationId == locationId && r.Name.ToLower() == name.ToLower() && r.Id != exceptId);
        if (taken)
        {
            throw NameTaken();
        }
    }

    // The database is the final guard if two requests race past the checks above.
    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Resources_LocationId_Name" })
        {
            throw NameTaken();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // The location or type was deleted after the checks above.
            var constraint = ((PostgresException)ex.InnerException!).ConstraintName;
            throw ApiException.Invalid(new Dictionary<string, string[]>
            {
                [FieldForForeignKey(constraint)] = ["That no longer exists. Reload the lists, choose again and try again."],
            });
        }
    }

    // Which request field a foreign key on Resources belongs to.
    public static string FieldForForeignKey(string? constraintName) =>
        constraintName == "FK_Resources_ResourceTypes_ResourceTypeId" ? "resourceTypeId" : "locationId";
}
