using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Jobs;

// Every minute, bookings that have ended move from Confirmed or CheckedIn to Completed.
public class CompleteEndedBookings : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<CompleteEndedBookings> _logger;

    public CompleteEndedBookings(IServiceScopeFactory scopes, IClock clock, ILogger<CompleteEndedBookings> logger)
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
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var completed = await RunOnceAsync(db, _clock.UtcNow);
                if (completed > 0)
                {
                    _logger.LogInformation("Marked {Count} ended bookings as completed.", completed);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Completing ended bookings failed; trying again next minute.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Compare-and-set: only bookings still Confirmed or CheckedIn change, so running it twice is harmless.
    public static Task<int> RunOnceAsync(AppDbContext db, DateTime now) =>
        db.Bookings
            .Where(b => (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) && b.End <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.Completed)
                .SetProperty(b => b.Version, b => b.Version + 1));
}
