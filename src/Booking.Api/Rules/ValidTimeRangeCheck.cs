using Booking.Api.Domain;

namespace Booking.Api.Rules;

public class ValidTimeRangeCheck : IBookingCheck
{
    public IEnumerable<Violation> Check(BookingCandidate booking)
    {
        var range = booking.Range;
        if (range.End <= range.Start)
        {
            yield return new Violation("END_BEFORE_START", "The end time must be after the start time. Choose a later end time.");
        }
        // Starting exactly now is fine; a minute ago is not.
        if (range.Start < booking.Now)
        {
            yield return new Violation("IN_THE_PAST", "The booking starts in the past. Choose a start time from now on.");
        }
        if (!IsWholeMinute(range.Start) || !IsWholeMinute(range.End))
        {
            yield return new Violation("NOT_WHOLE_MINUTES", "Start and end must be whole minutes, for example 10:00 or 10:15.");
        }
    }

    private static bool IsWholeMinute(DateTime time) => time.Ticks % TimeSpan.TicksPerMinute == 0;
}
