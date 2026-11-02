using Booking.Api.Abstractions;
using Booking.Api.Domain;

namespace Booking.Api.Infrastructure;

// Notifications stored in the database and shown under the bell.
public class InAppNotifier : INotifier
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public InAppNotifier(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // Only adds the row: the caller's SaveChanges stores it together with the change it describes.
    public void Notify(Guid userId, string message) =>
        _db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Message = message, CreatedAt = _clock.UtcNow });
}
