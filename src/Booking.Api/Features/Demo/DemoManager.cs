using Booking.Api.Infrastructure;

namespace Booking.Api.Features.Demo;

public class DemoManager
{
    private readonly AppDbContext _db;

    public DemoManager(AppDbContext db)
    {
        _db = db;
    }

    public Task ResetAsync() => DemoData.ResetAsync(_db);
}
