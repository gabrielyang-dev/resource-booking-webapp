using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Jobs;

// Every minute, Pending bookings that reach their start time without a decision are rejected,
// so the slot is freed and the owner is told.
public class ExpireApprovals : BackgroundService
{
    public const string Reason = "No decision was made before the start time.";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<ExpireApprovals> _logger;

    public ExpireApprovals(IServiceScopeFactory scopes, IClock clock, ILogger<ExpireApprovals> logger)
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
                var expired = await RunOnceAsync(
                    scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<INotifier>(), _clock.UtcNow);
                if (expired > 0)
                {
                    _logger.LogInformation("Rejected {Count} bookings that were never approved.", expired);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Expiring approvals failed; trying again next minute.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Compare-and-set per booking, so only the ones this run actually changed are notified. Running it twice is harmless.
    public static async Task<int> RunOnceAsync(AppDbContext db, INotifier notifier, DateTime now)
    {
        var due = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending && b.Start <= now)
            .Select(b => new { b.Id, b.OwnerId, ResourceName = b.Resource.Name })
            .ToListAsync();

        var expired = 0;
        foreach (var booking in due)
        {
            var updated = await db.Bookings
                .Where(b => b.Id == booking.Id && b.Status == BookingStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, BookingStatus.Rejected)
                    .SetProperty(b => b.RejectionReason, Reason)
                    .SetProperty(b => b.Version, b => b.Version + 1));
            if (updated == 1)
            {
                notifier.Notify(booking.OwnerId, $"Your {booking.ResourceName} request was not approved in time. {Reason} Book again or choose another resource.");
                expired++;
            }
        }
        await db.SaveChangesAsync();
        return expired;
    }
}
