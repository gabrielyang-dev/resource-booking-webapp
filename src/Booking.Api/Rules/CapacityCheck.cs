using Booking.Api.Domain;

namespace Booking.Api.Rules;

public class CapacityCheck : IBookingCheck
{
    public IEnumerable<Violation> Check(BookingCandidate booking)
    {
        // No capacity (equipment, for example) means no people limit.
        if (booking.Capacity is int capacity && booking.Attendees is int attendees && attendees > capacity)
        {
            yield return new Violation("OVER_CAPACITY",
                $"{booking.ResourceName} holds {capacity} people, but you asked for {attendees}. Choose a bigger resource or fewer attendees.");
        }
    }
}
