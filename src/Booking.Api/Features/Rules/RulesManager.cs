using System.Text.Json;
using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Infrastructure;
using Booking.Api.Rules;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.Rules;

// Reads and replaces the rules set on resource types (defaults) and resources (overrides).
public class RulesManager
{
    private readonly AppDbContext _db;
    private readonly RuleEngine _engine;

    public RulesManager(AppDbContext db, RuleEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<IReadOnlyList<RuleResponse>> GetForTypeAsync(Guid typeId)
    {
        await EnsureTypeExistsAsync(typeId);
        var rows = await _db.RuleSettings.AsNoTracking()
            .Where(s => s.ResourceTypeId == typeId)
            .OrderBy(s => s.Kind)
            .ToListAsync();
        return rows.Select(s => RuleResponse.From(s, "type")).ToList();
    }

    // Every kind that applies to the resource, saying whether it comes from the type or the resource.
    public async Task<IReadOnlyList<RuleResponse>> GetForResourceAsync(Guid resourceId)
    {
        var resource = await _db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resourceId)
            ?? throw ApiException.NotFound("There is no resource with that id. Check the id and try again.");
        var rows = await LoadEffectiveRowsAsync(resource.Id, resource.ResourceTypeId);
        return rows.Select(s => RuleResponse.From(s, s.ResourceId is null ? "inherited" : "overridden")).ToList();
    }

    public async Task<IReadOnlyList<RuleResponse>> ReplaceForTypeAsync(Guid typeId, IReadOnlyList<RuleRequest>? rules)
    {
        await EnsureTypeExistsAsync(typeId);
        var settings = Validate(rules);
        await ReplaceAsync(_db.RuleSettings.Where(s => s.ResourceTypeId == typeId),
            settings.Select(s => new RuleSetting { Id = Guid.NewGuid(), ResourceTypeId = typeId, Kind = s.Kind, Settings = s.Json }));
        return await GetForTypeAsync(typeId);
    }

    public async Task<IReadOnlyList<RuleResponse>> ReplaceForResourceAsync(Guid resourceId, IReadOnlyList<RuleRequest>? rules)
    {
        if (!await _db.Resources.AnyAsync(r => r.Id == resourceId))
        {
            throw ApiException.NotFound("There is no resource with that id. Check the id and try again.");
        }
        var settings = Validate(rules);
        await ReplaceAsync(_db.RuleSettings.Where(s => s.ResourceId == resourceId),
            settings.Select(s => new RuleSetting { Id = Guid.NewGuid(), ResourceId = resourceId, Kind = s.Kind, Settings = s.Json }));
        return await GetForResourceAsync(resourceId);
    }

    // The settings JSON per kind that applies to a resource: its override if it has one, else its type's default.
    // Used by BookingManager and the insights.
    public async Task<Dictionary<RuleKind, string>> GetEffectiveAsync(Guid resourceId, Guid resourceTypeId)
    {
        var rows = await LoadEffectiveRowsAsync(resourceId, resourceTypeId);
        return rows.ToDictionary(s => s.Kind, s => s.Settings);
    }

    private async Task<List<RuleSetting>> LoadEffectiveRowsAsync(Guid resourceId, Guid resourceTypeId)
    {
        var rows = await _db.RuleSettings.AsNoTracking()
            .Where(s => s.ResourceId == resourceId || s.ResourceTypeId == resourceTypeId)
            .ToListAsync();
        // For each kind, the resource's own row wins over the type's.
        return rows
            .GroupBy(s => s.Kind)
            .Select(g => g.FirstOrDefault(s => s.ResourceId is not null) ?? g.First())
            .OrderBy(s => s.Kind)
            .ToList();
    }

    private async Task ReplaceAsync(IQueryable<RuleSetting> existing, IEnumerable<RuleSetting> replacements)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await existing.ExecuteDeleteAsync();
        _db.RuleSettings.AddRange(replacements);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private List<(RuleKind Kind, string Json)> Validate(IReadOnlyList<RuleRequest>? rules)
    {
        var errors = new Dictionary<string, string[]>();
        var result = new List<(RuleKind, string)>();
        if (rules is null)
        {
            throw ApiException.Invalid(new() { ["rules"] = ["Send a list of rules (an empty list removes them all)."] });
        }
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!Enum.TryParse<RuleKind>(rule.Kind, ignoreCase: false, out var kind) || !Enum.IsDefined(kind)
                || int.TryParse(rule.Kind, out _))
            {
                errors[$"rules[{i}]"] = ["The kind must be OpeningHours, MaxDuration, BookingWindow or DailyQuota."];
                continue;
            }
            if (rule.Settings is null || rule.Settings.Value.ValueKind != JsonValueKind.Object)
            {
                errors[$"rules[{i}]"] = ["Each rule needs settings, as a JSON object."];
                continue;
            }
            var json = rule.Settings.Value.GetRawText();
            var problem = _engine.ValidateSettings(kind, json);
            if (problem is not null)
            {
                errors[$"rules[{i}]"] = [problem];
                continue;
            }
            result.Add((kind, json));
        }
        if (result.Select(r => r.Item1).Distinct().Count() != result.Count)
        {
            errors["rules"] = ["Each kind of rule can appear only once."];
        }
        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
        return result;
    }

    private async Task EnsureTypeExistsAsync(Guid typeId)
    {
        if (!await _db.ResourceTypes.AnyAsync(t => t.Id == typeId))
        {
            throw ApiException.NotFound("There is no resource type with that id. Check the id and try again.");
        }
    }
}
