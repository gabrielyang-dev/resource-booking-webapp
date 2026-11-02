namespace Booking.Api.Domain;

// Pending, Confirmed and CheckedIn hold the slot; the rest free it. This PR only produces Confirmed.
public enum BookingStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled,
    Rejected,
    Released,
}
