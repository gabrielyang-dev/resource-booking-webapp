using Booking.Api.Domain;

namespace Booking.Api.Features.Bookings;

// Pure search for other times on the same day: same length, in fixed steps, nearest to the request first
// (ties go to the earlier time). isValid decides whether a time is free and passes the rules.
public static class AlternativeTimes
{
    public static List<TimeRange> Find(TimeRange requested, TimeRange day, TimeSpan step, int max, Func<TimeRange, bool> isValid)
    {
        var length = requested.End - requested.Start;
        var candidates = new List<TimeRange>();
        for (var start = day.Start; start + length <= day.End; start += step)
        {
            if (start != requested.Start)
            {
                candidates.Add(new TimeRange(start, start + length));
            }
        }

        return candidates
            .OrderBy(c => Math.Abs((c.Start - requested.Start).Ticks))
            .ThenBy(c => c.Start)
            .Where(isValid)
            .Take(max)
            .ToList();
    }
}
