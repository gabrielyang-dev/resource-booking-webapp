using Booking.Api.Domain;

namespace Booking.Api.Rules;

// Check-in settings for a resource type or resource. It never refuses a booking; it decides when
// the owner can check in, and, when check-in is required, when a no-show is released.
public class CheckInRule : IBookingRule
{
    // Without a check-in rule, owners may still check in from 15 minutes before the start until the end.
    public static readonly CheckInSettings Default = new(Required: false, OpensMinutesBefore: 15, ReleaseAfterMinutes: 15);

    public RuleKind Kind => RuleKind.CheckIn;

    public IEnumerable<Violation> Check(BookingCandidate booking, string settingsJson) => [];

    public string? ValidateSettings(string settingsJson)
    {
        var settings = RuleSettings.Parse<CheckInSettings>(settingsJson);
        if (settings is null || settings.OpensMinutesBefore is < 0 or > 24 * 60 || settings.ReleaseAfterMinutes is < 1 or > 24 * 60)
        {
            return "Check-in must open 0 to 1,440 minutes before the start, and release no-shows 1 to 1,440 minutes after it.";
        }
        return null;
    }

    // When check-in opens and closes for a booking: [opens, closes).
    // If check-in is required it closes when a no-show is released; otherwise at the end of the booking.
    public static TimeRange Window(TimeRange booking, CheckInSettings settings)
    {
        var opens = booking.Start.AddMinutes(-settings.OpensMinutesBefore);
        var closes = settings.Required ? ReleaseAt(booking, settings) : booking.End;
        return new TimeRange(opens, closes);
    }

    // When an unchecked booking is released (only when check-in is required). Never later than the end.
    public static DateTime ReleaseAt(TimeRange booking, CheckInSettings settings)
    {
        var release = booking.Start.AddMinutes(settings.ReleaseAfterMinutes);
        return release < booking.End ? release : booking.End;
    }
}
