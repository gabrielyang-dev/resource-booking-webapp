namespace Booking.Api.Domain;

public class Booking
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    // UTC, half-open [Start, End).
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public BookingStatus Status { get; set; }
    public int? Attendees { get; set; }

    // Concurrency token for edits (used from the reschedule PR on).
    public int Version { get; set; } = 1;

    // Set when the client sent an Idempotency-Key; unique per owner.
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }

    // When the owner checked in (UTC). Kept after the booking completes, so insights can tell no-shows apart.
    public DateTime? CheckedInAt { get; set; }

    // Why the approver said no (or "no decision before the start"), shown to the owner.
    public string? RejectionReason { get; set; }

    // Add-ons ordered for this booking, in any status.
    public List<AddOnOrder> AddOnOrders { get; set; } = [];
}
