using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Rules;

[ApiController]
[Authorize]
public class RulesController : ControllerBase
{
    private readonly RulesManager _rules;

    public RulesController(RulesManager rules)
    {
        _rules = rules;
    }

    [HttpGet("api/resource-types/{id:guid}/rules")]
    public async Task<IReadOnlyList<RuleResponse>> GetForType(Guid id) => await _rules.GetForTypeAsync(id);

    // Replaces all of the type's rules with the list sent.
    [HttpPut("api/resource-types/{id:guid}/rules")]
    [Authorize(Roles = "Admin")]
    public async Task<IReadOnlyList<RuleResponse>> ReplaceForType(Guid id, List<RuleRequest> rules) =>
        await _rules.ReplaceForTypeAsync(id, rules);

    [HttpGet("api/resources/{id:guid}/rules")]
    public async Task<IReadOnlyList<RuleResponse>> GetForResource(Guid id) => await _rules.GetForResourceAsync(id);

    // Replaces the resource's overrides; an empty list goes back to the type's rules.
    [HttpPut("api/resources/{id:guid}/rules")]
    [Authorize(Roles = "Admin")]
    public async Task<IReadOnlyList<RuleResponse>> ReplaceForResource(Guid id, List<RuleRequest> rules) =>
        await _rules.ReplaceForResourceAsync(id, rules);
}
