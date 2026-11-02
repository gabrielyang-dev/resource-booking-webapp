using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Features.Catalog;

public class ResourceTypesManager
{
    private readonly AppDbContext _db;

    public ResourceTypesManager(AppDbContext db)
    {
        _db = db;
    }

    public Task<PagedResponse<ResourceTypeResponse>> ListAsync(int page, int pageSize)
    {
        var ordered = _db.ResourceTypes.AsNoTracking().OrderBy(t => t.Name).ThenBy(t => t.Id);
        return Paging.ToPageAsync(ordered, page, pageSize, ResourceTypeResponse.From);
    }

    public async Task<ResourceTypeResponse> GetAsync(Guid id)
    {
        var type = await _db.ResourceTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return ResourceTypeResponse.From(type ?? throw TypeNotFound());
    }

    public async Task<ResourceTypeResponse> CreateAsync(ResourceTypeRequest request)
    {
        var name = Validate(request);
        await EnsureNameIsFree(name, exceptId: null);

        var type = new ResourceType { Id = Guid.NewGuid(), Name = name };
        _db.ResourceTypes.Add(type);
        await SaveAsync();
        return ResourceTypeResponse.From(type);
    }

    public async Task<ResourceTypeResponse> UpdateAsync(Guid id, ResourceTypeRequest request)
    {
        var name = Validate(request);
        var type = await _db.ResourceTypes.FirstOrDefaultAsync(t => t.Id == id) ?? throw TypeNotFound();
        await EnsureNameIsFree(name, exceptId: id);

        type.Name = name;
        await SaveAsync();
        return ResourceTypeResponse.From(type);
    }

    public async Task DeleteAsync(Guid id)
    {
        var type = await _db.ResourceTypes.FirstOrDefaultAsync(t => t.Id == id) ?? throw TypeNotFound();
        var used = await _db.Resources.CountAsync(r => r.ResourceTypeId == id);
        if (used > 0)
        {
            throw InUse(type.Name, used);
        }

        _db.ResourceTypes.Remove(type);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // A resource was added after the count above; the foreign key still refuses the delete.
            throw InUse(type.Name, await _db.Resources.CountAsync(r => r.ResourceTypeId == id));
        }
    }

    private static ApiException TypeNotFound() =>
        ApiException.NotFound("There is no resource type with that id. Check the id and try again.");

    private static ApiException InUse(string name, int count) =>
        ApiException.Conflict("IN_USE",
            $"{name} is used by {count} resource{(count == 1 ? "" : "s")} (active or inactive). Change their type first, then delete the resource type.");

    private static ApiException NameTaken() =>
        ApiException.Conflict("NAME_TAKEN", "A resource type with that name already exists. Use a different name.");

    private static string Validate(ResourceTypeRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = CatalogValidation.CleanName(request.Name, errors);
        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
        return name;
    }

    private async Task EnsureNameIsFree(string name, Guid? exceptId)
    {
        var taken = await _db.ResourceTypes.AnyAsync(t => t.Name.ToLower() == name.ToLower() && t.Id != exceptId);
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_ResourceTypes_Name" })
        {
            throw NameTaken();
        }
    }
}
