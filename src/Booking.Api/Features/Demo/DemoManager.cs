using Booking.Api.Abstractions;
using Booking.Api.Infrastructure;

namespace Booking.Api.Features.Demo;

public class DemoManager
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public DemoManager(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // Restores the seed, with demo bookings around the current time.
    public Task ResetAsync() => DemoData.ResetAsync(_db, _clock.UtcNow);
}
