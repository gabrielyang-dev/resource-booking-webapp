using Booking.Api.Domain;

namespace Booking.Api.Rules;

// How soon (minimum notice) and how far ahead (maximum days) a booking can start.
public class BookingWindowRule : IBookingRule
{
    public RuleKind Kind => RuleKind.BookingWindow;

    public IEnumerable<Violation> Check(BookingCandidate booking, string settingsJson)
    {
        var settings = RuleSettings.Parse<BookingWindowSettings>(settingsJson);
        if (settings is null)
        {
            yield break;
        }
        var notice = TimeSpan.FromMinutes(settings.MinNoticeMinutes);
        if (booking.Range.Start - booking.Now < notice)
        {
            yield return new Violation("TOO_LITTLE_NOTICE",
                $"{booking.ResourceName} needs {RuleSettings.Duration(notice)} notice. Choose a later start time.");
        }
        if (booking.Range.Start > booking.Now.AddDays(settings.MaxDaysAhead))
        {
            yield return new Violation("TOO_FAR_AHEAD",
                $"{booking.ResourceName} can be booked at most {settings.MaxDaysAhead} days ahead. Choose an earlier date.");
        }
    }

    public string? ValidateSettings(string settingsJson)
    {
        var settings = RuleSettings.Parse<BookingWindowSettings>(settingsJson);
        if (settings is null || settings.MinNoticeMinutes < 0 || settings.MaxDaysAhead < 1)
        {
            return "Minimum notice must be 0 minutes or more, and maximum days ahead at least 1.";
        }
        return null;
    }
}
