using Booking.Api.Domain;

namespace Booking.Api.Rules;

public class MaxDurationRule : IBookingRule
{
    public RuleKind Kind => RuleKind.MaxDuration;

    public IEnumerable<Violation> Check(BookingCandidate booking, string settingsJson)
    {
        var settings = RuleSettings.Parse<MaxDurationSettings>(settingsJson);
        if (settings is null)
        {
            yield break;
        }
        var length = booking.Range.End - booking.Range.Start;
        var max = TimeSpan.FromMinutes(settings.MaxMinutes);
        if (length > max)
        {
            yield return new Violation("TOO_LONG",
                $"Bookings on {booking.ResourceName} can last at most {RuleSettings.Duration(max)}. Yours lasts {RuleSettings.Duration(length)}. Shorten it or split it in two.");
        }
    }

    public string? ValidateSettings(string settingsJson)
    {
        var settings = RuleSettings.Parse<MaxDurationSettings>(settingsJson);
        return settings is null || settings.MaxMinutes < 1 ? "The maximum duration must be at least 1 minute." : null;
    }
}
