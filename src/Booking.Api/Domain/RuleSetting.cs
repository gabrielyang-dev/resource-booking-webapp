namespace Booking.Api.Domain;

// A configured rule. Set on a resource type (the default for its resources)
// or on one resource (an override). Exactly one of the two ids is set.
public class RuleSetting
{
    public Guid Id { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public Guid? ResourceId { get; set; }
    public RuleKind Kind { get; set; }

    // JSON, shaped by the kind's settings record in Rules/RuleSettings.cs.
    public string Settings { get; set; } = "{}";
}
