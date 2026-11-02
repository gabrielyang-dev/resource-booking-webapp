using Booking.Api.Domain;
using Booking.Api.Rules;

namespace Booking.Api.Features.Insights;

// Utilisation = booked hours / open hours. Pure maths: no database, so it is easy to test.
public static class UtilisationCalculator
{
    // Pending, Cancelled, Rejected and Released never count as used.
    public static readonly BookingStatus[] CountedStatuses =
        [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed];

    // Hours of the bookings inside the period. A booking running past either end counts only its hours inside.
    public static double BookedHours(IEnumerable<(TimeRange Range, BookingStatus Status)> bookings, TimeRange period)
    {
        double hours = 0;
        foreach (var (range, status) in bookings)
        {
            if (!CountedStatuses.Contains(status))
            {
                continue;
            }
            var start = range.Start > period.Start ? range.Start : period.Start;
            var end = range.End < period.End ? range.End : period.End;
            if (end > start)
            {
                hours += (end - start).TotalHours;
            }
        }
        return hours;
    }

    // Open hours for each local day from firstDay to lastDay (inclusive), in the location's time zone.
    // With no opening-hours rule a resource is open all day: 24 hours, or 23 and 25 when the clocks change.
    public static double OpenHours(DateOnly firstDay, DateOnly lastDay, TimeZoneInfo zone, OpeningHoursSettings? openingHours)
    {
        double hours = 0;
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            if (openingHours is null)
            {
                var day = LocalTime.Day(date, zone);
                hours += (day.End - day.Start).TotalHours;
            }
            else if (openingHours.Days.Contains(RuleSettings.DayName(date.DayOfWeek))
                && RuleSettings.TryParseTime(openingHours.Opens, out var opens)
                && RuleSettings.TryParseTime(openingHours.Closes, out var closes))
            {
                hours += (LocalTime.ToUtc(date, closes, zone) - LocalTime.ToUtc(date, opens, zone)).TotalHours;
            }
        }
        return hours;
    }

    public static double Ratio(double booked, double open) => open <= 0 ? 0 : booked / open;
}
