using Booking.Api.Domain;

namespace Booking.Api.Features.Catalog;

public record LocationRequest(string? Name, string? TimeZone);

public record LocationResponse(Guid Id, string Name, string TimeZone)
{
    public static LocationResponse From(Location location) => new(location.Id, location.Name, location.TimeZone);
}

public record ResourceTypeRequest(string? Name);

public record ResourceTypeResponse(Guid Id, string Name)
{
    public static ResourceTypeResponse From(ResourceType type) => new(type.Id, type.Name);
}

// Active is optional on create (true when left out) and required on update, like every other field there.
// ApproverId: the user who must approve bookings on this resource; empty means bookings are confirmed straight away.
public record ResourceRequest(Guid? LocationId, Guid? ResourceTypeId, string? Name, int? Capacity, bool? Active, Guid? ApproverId = null);

// Carries the location and type names so the UI needs no extra calls.
public record ResourceResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    string TimeZone,
    Guid ResourceTypeId,
    string ResourceTypeName,
    string Name,
    int? Capacity,
    bool Active,
    Guid? ApproverId)
{
    // The resource must have been loaded with its Location and ResourceType.
    public static ResourceResponse From(Resource r) => new(
        r.Id, r.LocationId, r.Location.Name, r.Location.TimeZone,
        r.ResourceTypeId, r.ResourceType.Name, r.Name, r.Capacity, r.IsActive, r.ApproverId);
}
