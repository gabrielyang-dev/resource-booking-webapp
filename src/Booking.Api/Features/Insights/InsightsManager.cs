using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Bookings;
using Booking.Api.Features.Rules;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Booking.Api.Rules;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.Insights;

public class InsightsManager
{
    private const int MaxDays = 366;

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly RulesManager _rules;

    public InsightsManager(AppDbContext db, IClock clock, RulesManager rules)
    {
        _db = db;
        _clock = clock;
        _rules = rules;
    }

    // Booked hours / open hours per resource and per type, for local dates from..to (both included).
    // Defaults to the current week, Monday to Sunday.
    public async Task<UtilisationResponse> GetUtilisationAsync(DateOnly? from, DateOnly? to, Guid? locationId, Guid? resourceTypeId)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var first = from ?? monday;
        var last = to ?? first.AddDays(6);
        if (last < first || last.DayNumber - first.DayNumber >= MaxDays)
        {
            throw ApiException.Invalid(new() { ["to"] = [$"The end date must be on or after the start date, and at most {MaxDays} days later."] });
        }

        var resources = await _db.Resources.AsNoTracking()
            .Include(r => r.Location).Include(r => r.ResourceType)
            .Where(r => r.IsActive)
            .Where(r => locationId == null || r.LocationId == locationId)
            .Where(r => resourceTypeId == null || r.ResourceTypeId == resourceTypeId)
            .OrderBy(r => r.Name).ThenBy(r => r.Id)
            .ToListAsync();

        // One query for all bookings that could touch the range in any time zone (a day of slack each side).
        var ids = resources.Select(r => r.Id).ToList();
        var windowStart = first.AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEnd = last.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.ResourceId) && UtilisationCalculator.CountedStatuses.Contains(b.Status)
                && b.Start < windowEnd && windowStart < b.End)
            .Select(b => new { b.ResourceId, b.Start, b.End, b.Status })
            .ToListAsync();

        var rows = new List<ResourceUtilisation>();
        foreach (var resource in resources)
        {
            var zone = LocalTime.Zone(resource.Location.TimeZone);
            var period = new TimeRange(LocalTime.Day(first, zone).Start, LocalTime.Day(last, zone).End);
            var settings = await _rules.GetEffectiveAsync(resource.Id, resource.ResourceTypeId);
            var openingHours = settings.TryGetValue(RuleKind.OpeningHours, out var json)
                ? RuleSettings.Parse<OpeningHoursSettings>(json)
                : null;

            var booked = UtilisationCalculator.BookedHours(
                bookings.Where(b => b.ResourceId == resource.Id).Select(b => (new TimeRange(b.Start, b.End), b.Status)),
                period);
            var open = UtilisationCalculator.OpenHours(first, last, zone, openingHours);
            rows.Add(new ResourceUtilisation(resource.Id, resource.Name, resource.ResourceTypeId, resource.ResourceType.Name,
                resource.Location.Name, Math.Round(booked, 2), Math.Round(open, 2), UtilisationCalculator.Ratio(booked, open)));
        }

        var types = rows
            .GroupBy(r => new { r.ResourceTypeId, r.ResourceTypeName })
            .Select(g => new TypeUtilisation(g.Key.ResourceTypeId, g.Key.ResourceTypeName,
                g.Sum(r => r.BookedHours), g.Sum(r => r.OpenHours),
                UtilisationCalculator.Ratio(g.Sum(r => r.BookedHours), g.Sum(r => r.OpenHours))))
            .OrderBy(t => t.Name)
            .ToList();

        return new UtilisationResponse(first, last, rows, types);
    }

    // Every booking with any status, newest first, filtered by resource, owner, status and start time.
    public Task<PagedResponse<BookingResponse>> GetHistoryAsync(
        Guid? resourceId, Guid? userId, string? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize)
    {
        BookingStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<BookingStatus>(status, out var s) || int.TryParse(status, out _))
            {
                throw ApiException.Invalid(new() { ["status"] = ["Use a status such as Confirmed, Completed or Cancelled."] });
            }
            parsedStatus = s;
        }
        var fromUtc = from?.UtcDateTime;
        var toUtc = to?.UtcDateTime;

        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Resource).ThenInclude(r => r.Location).Include(b => b.Owner)
            .Where(b => resourceId == null || b.ResourceId == resourceId)
            .Where(b => userId == null || b.OwnerId == userId)
            .Where(b => parsedStatus == null || b.Status == parsedStatus)
            .Where(b => fromUtc == null || b.Start >= fromUtc)
            .Where(b => toUtc == null || b.Start < toUtc)
            .OrderByDescending(b => b.Start).ThenBy(b => b.Id);
        return Paging.ToPageAsync(query, page, pageSize, BookingResponse.From);
    }
}
