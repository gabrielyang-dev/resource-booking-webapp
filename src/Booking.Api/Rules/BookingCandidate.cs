using Booking.Api.Domain;

namespace Booking.Api.Rules;

// Everything the checks and rules need to judge one booking, loaded by BookingManager.
// Rules never touch the database: what they need is passed in here.
public record BookingCandidate(
    string ResourceName,
    int? Capacity,
    string TimeZone,
    Guid OwnerId,
    TimeRange Range,
    int? Attendees,
    DateTime Now,
    // The owner's other slot-holding bookings on resources of the same type, around the same day (for quotas).
    IReadOnlyList<TimeRange> OwnerOtherBookings);
