using Booking.Api.Domain;

namespace Booking.Api.Rules;

// How many minutes one owner may hold resources of this type on one local day.
// Judged for the booking's owner, even when an admin makes the booking.
public class DailyQuotaRule : IBookingRule
{
    public RuleKind Kind => RuleKind.DailyQuota;

    public IEnumerable<Violation> Check(BookingCandidate booking, string settingsJson)
    {
        var settings = RuleSettings.Parse<DailyQuotaSettings>(settingsJson);
        if (settings is null)
        {
            yield break;
        }
        var zone = LocalTime.Zone(booking.TimeZone);
        var date = DateOnly.FromDateTime(LocalTime.ToLocal(booking.Range.Start, zone));
        var day = LocalTime.Day(date, zone);

        var used = booking.OwnerOtherBookings.Sum(b => MinutesInside(b, day));
        var total = used + MinutesInside(booking.Range, day);
        if (total > settings.MaxMinutesPerDay)
        {
            var max = TimeSpan.FromMinutes(settings.MaxMinutesPerDay);
            yield return new Violation("DAILY_QUOTA_EXCEEDED",
                $"Each person can book {RuleSettings.Duration(max)} of this kind of resource per day. " +
                $"This would make {RuleSettings.Duration(TimeSpan.FromMinutes(total))} on {date:d MMM}. Shorten it or pick another day.");
        }
    }

    private static double MinutesInside(TimeRange range, TimeRange day)
    {
        var start = range.Start > day.Start ? range.Start : day.Start;
        var end = range.End < day.End ? range.End : day.End;
        return end > start ? (end - start).TotalMinutes : 0;
    }

    public string? ValidateSettings(string settingsJson)
    {
        var settings = RuleSettings.Parse<DailyQuotaSettings>(settingsJson);
        return settings is null || settings.MaxMinutesPerDay < 1 ? "The daily quota must be at least 1 minute." : null;
    }
}
