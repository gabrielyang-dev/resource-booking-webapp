using Booking.Api.Domain;

namespace Booking.Api.Rules;

// A rule an admin configures per resource type or resource. Settings are the stored JSON.
public interface IBookingRule
{
    RuleKind Kind { get; }

    // Returns nothing when the booking passes.
    IEnumerable<Violation> Check(BookingCandidate booking, string settingsJson);

    // Returns a message saying what is wrong with the settings, or null when they are valid.
    string? ValidateSettings(string settingsJson);
}
