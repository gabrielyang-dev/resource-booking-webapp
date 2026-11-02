using Booking.Api.Domain;

namespace Booking.Api.Rules;

// An add-on ordered with the booking being judged, with what the checks need to know about it.
public record OrderedAddOn(string Name, int LeadTimeMinutes, TimeOnly AvailableFrom, TimeOnly AvailableUntil, int Quantity);

// Each add-on must be ordered at least its lead time before the booking starts.
public class AddOnLeadTimeCheck : IBookingCheck
{
    public IEnumerable<Violation> Check(BookingCandidate booking)
    {
        foreach (var addOn in booking.AddOns ?? [])
        {
            var deadline = booking.Range.Start.AddMinutes(-addOn.LeadTimeMinutes);
            if (booking.Now > deadline)
            {
                var local = LocalTime.ToLocal(deadline, LocalTime.Zone(booking.TimeZone));
                yield return new Violation("ADD_ON_TOO_LATE",
                    $"{addOn.Name} need{(addOn.Name.EndsWith('s') ? "" : "s")} {Notice(addOn.LeadTimeMinutes)} notice. " +
                    $"The last time to order for this booking was {local:ddd d MMM HH:mm}. Move the booking later or remove {addOn.Name}.");
            }
        }
    }

    // "24 hours'" or "1 hour's", as in "24 hours' notice".
    private static string Notice(int minutes)
    {
        var text = RuleSettings.Duration(TimeSpan.FromMinutes(minutes));
        return text.EndsWith('s') ? text + "'" : text + "'s";
    }
}

// Each add-on can only be delivered when the booking starts within its available hours, in local time.
public class AddOnHoursCheck : IBookingCheck
{
    public IEnumerable<Violation> Check(BookingCandidate booking)
    {
        var start = TimeOnly.FromDateTime(LocalTime.ToLocal(booking.Range.Start, LocalTime.Zone(booking.TimeZone)));
        foreach (var addOn in booking.AddOns ?? [])
        {
            if (start < addOn.AvailableFrom || start >= addOn.AvailableUntil)
            {
                yield return new Violation("ADD_ON_UNAVAILABLE",
                    $"{addOn.Name} {(addOn.Name.EndsWith('s') ? "are" : "is")} available for bookings starting {addOn.AvailableFrom:HH:mm} to {addOn.AvailableUntil:HH:mm}. " +
                    $"Yours starts at {start:HH:mm}. Change the time or remove {addOn.Name}.");
            }
        }
    }
}
