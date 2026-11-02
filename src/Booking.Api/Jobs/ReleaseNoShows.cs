using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Infrastructure;
using Booking.Api.Rules;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Jobs;

// Every minute, bookings on resources that require check-in, and that nobody checked in to, are released:
// status Released frees the slot so someone else can book it.
public class ReleaseNoShows : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<ReleaseNoShows> _logger;

    public ReleaseNoShows(IServiceScopeFactory scopes, IClock clock, ILogger<ReleaseNoShows> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var released = await RunOnceAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), _clock.UtcNow);
                if (released > 0)
                {
                    _logger.LogInformation("Released {Count} bookings nobody checked in to.", released);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Releasing no-shows failed; trying again next minute.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Compare-and-set: only bookings still Confirmed (not checked in, not cancelled) change, so running it twice is harmless.
    public static async Task<int> RunOnceAsync(AppDbContext db, DateTime now)
    {
        var checkInRules = await db.RuleSettings.AsNoTracking().Where(s => s.Kind == RuleKind.CheckIn).ToListAsync();
        if (checkInRules.Count == 0)
        {
            return 0;
        }

        var started = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed && b.Start <= now)
            .Select(b => new { b.Id, b.ResourceId, b.Resource.ResourceTypeId, b.Start, b.End })
            .ToListAsync();

        var toRelease = new List<Guid>();
        foreach (var booking in started)
        {
            // The resource's own rule wins over its type's, as everywhere else.
            var rule = checkInRules.FirstOrDefault(r => r.ResourceId == booking.ResourceId)
                ?? checkInRules.FirstOrDefault(r => r.ResourceTypeId == booking.ResourceTypeId);
            var settings = rule is null ? null : RuleSettings.Parse<CheckInSettings>(rule.Settings);
            if (settings is { Required: true } && CheckInRule.ReleaseAt(new TimeRange(booking.Start, booking.End), settings) <= now)
            {
                toRelease.Add(booking.Id);
            }
        }
        if (toRelease.Count == 0)
        {
            return 0;
        }

        return await db.Bookings
            .Where(b => toRelease.Contains(b.Id) && b.Status == BookingStatus.Confirmed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.Released)
                .SetProperty(b => b.Version, b => b.Version + 1));
    }
}
