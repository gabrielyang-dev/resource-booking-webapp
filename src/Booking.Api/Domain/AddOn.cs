namespace Booking.Api.Domain;

// Something ordered for a booking, like catering. It is not booked for a time range itself:
// it needs notice, and can only be delivered at certain local hours.
public class AddOn
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;
    public string Name { get; set; } = "";

    // How long before the booking starts it must be ordered.
    public int LeadTimeMinutes { get; set; }

    // Local times at the location: the booking must start from AvailableFrom and before AvailableUntil.
    public TimeOnly AvailableFrom { get; set; }
    public TimeOnly AvailableUntil { get; set; }

    // Who delivers it (a user, not a role).
    public Guid? FulfillerId { get; set; }

    // Deactivated, never deleted, so old orders keep their add-on.
    public bool IsActive { get; set; } = true;
}

public enum AddOnOrderStatus
{
    Requested,
    Delivered,
    Cancelled,
}

// One add-on ordered for one booking.
public class AddOnOrder
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid AddOnId { get; set; }
    public AddOn AddOn { get; set; } = null!;
    public int Quantity { get; set; }
    public AddOnOrderStatus Status { get; set; }
}
