using Booking.Api.Domain;

namespace Booking.Api.Rules;

// The booking must start and end within the opening hours of one open day, on the location's local clock.
public class OpeningHoursRule : IBookingRule
{
    public RuleKind Kind => RuleKind.OpeningHours;

    public IEnumerable<Violation> Check(BookingCandidate booking, string settingsJson)
    {
        var settings = RuleSettings.Parse<OpeningHoursSettings>(settingsJson);
        if (settings is null || !RuleSettings.TryParseTime(settings.Opens, out var opens) || !RuleSettings.TryParseTime(settings.Closes, out var closes))
        {
            yield break;
        }

        var zone = LocalTime.Zone(booking.TimeZone);
        var start = LocalTime.ToLocal(booking.Range.Start, zone);
        var end = LocalTime.ToLocal(booking.Range.End, zone);

        if (!settings.Days.Contains(RuleSettings.DayName(start.DayOfWeek)))
        {
            yield return new Violation("CLOSED_DAY",
                $"{booking.ResourceName} can't be booked on a {start:dddd}. It is open on {string.Join(", ", settings.Days)}.");
            yield break;
        }
        if (end.Date != start.Date || TimeOnly.FromDateTime(start) < opens || TimeOnly.FromDateTime(end) > closes)
        {
            yield return new Violation("OUTSIDE_OPENING_HOURS",
                $"{booking.ResourceName} is open {settings.Opens} to {settings.Closes}. Your booking runs from {start:HH:mm} to {end:HH:mm}. Choose times within opening hours.");
        }
    }

    public string? ValidateSettings(string settingsJson)
    {
        var settings = RuleSettings.Parse<OpeningHoursSettings>(settingsJson);
        if (settings is null || settings.Days is null)
        {
            return "Opening hours need opens, closes and days.";
        }
        if (!RuleSettings.TryParseTime(settings.Opens, out var opens) || !RuleSettings.TryParseTime(settings.Closes, out var closes))
        {
            return "Opening and closing times must look like 08:00.";
        }
        if (closes <= opens)
        {
            return "The closing time must be after the opening time.";
        }
        if (settings.Days.Length == 0 || settings.Days.Any(d => !RuleSettings.AllDays.Contains(d)))
        {
            return "Choose at least one day, using Mon, Tue, Wed, Thu, Fri, Sat or Sun.";
        }
        return null;
    }
}
