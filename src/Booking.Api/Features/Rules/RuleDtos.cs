using System.Text.Json;
using Booking.Api.Domain;

namespace Booking.Api.Features.Rules;

// Kind is text ("OpeningHours", "MaxDuration", "BookingWindow", "DailyQuota") so a typo is a field error.
// Settings are the kind's JSON object, for example { "maxMinutes": 120 }.
public record RuleRequest(string? Kind, JsonElement? Settings);

// Source is "type" on a resource type's own list; on a resource it is "inherited" or "overridden".
public record RuleResponse(string Kind, JsonElement Settings, string Source)
{
    public static RuleResponse From(RuleSetting setting, string source) =>
        new(setting.Kind.ToString(), JsonDocument.Parse(setting.Settings).RootElement.Clone(), source);
}
