using Booking.Api.Domain;

namespace Booking.Api.Rules;

// The one place that decides whether a booking is allowed. Runs every check and every
// configured rule, and returns all the problems at once (an empty list means allowed).
public class RuleEngine
{
    private readonly IEnumerable<IBookingCheck> _checks;
    private readonly IEnumerable<IBookingRule> _rules;

    public RuleEngine(IEnumerable<IBookingCheck> checks, IEnumerable<IBookingRule> rules)
    {
        _checks = checks;
        _rules = rules;
    }

    // effectiveSettings: the settings JSON per kind that applies to this resource (override or type default).
    public List<Violation> Evaluate(BookingCandidate booking, IReadOnlyDictionary<RuleKind, string> effectiveSettings)
    {
        var violations = new List<Violation>();
        foreach (var check in _checks)
        {
            violations.AddRange(check.Check(booking));
        }
        foreach (var rule in _rules)
        {
            if (effectiveSettings.TryGetValue(rule.Kind, out var settings))
            {
                violations.AddRange(rule.Check(booking, settings));
            }
        }
        return violations;
    }

    public string? ValidateSettings(RuleKind kind, string settingsJson) =>
        _rules.First(r => r.Kind == kind).ValidateSettings(settingsJson);
}
