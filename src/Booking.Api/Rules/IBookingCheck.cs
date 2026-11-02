using Booking.Api.Domain;

namespace Booking.Api.Rules;

// A check that always runs, whatever the admin configured. Returns nothing when the booking passes.
public interface IBookingCheck
{
    IEnumerable<Violation> Check(BookingCandidate booking);
}
