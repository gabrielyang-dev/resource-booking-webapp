namespace Booking.Api.Domain;

public class Resource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;
    public Guid ResourceTypeId { get; set; }
    public ResourceType ResourceType { get; set; } = null!;

    // Unique within its location, ignoring case.
    public string Name { get; set; } = "";

    // How many people it holds. Null means "not a people limit", for example equipment.
    public int? Capacity { get; set; }

    // Resources are deactivated, never deleted, so bookings and history keep them.
    public bool IsActive { get; set; } = true;
}
