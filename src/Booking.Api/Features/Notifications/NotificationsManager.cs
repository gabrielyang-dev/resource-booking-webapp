using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.Notifications;

public record NotificationResponse(Guid Id, string Message, DateTime CreatedAt, bool Read)
{
    public static NotificationResponse From(Notification n) => new(n.Id, n.Message, n.CreatedAt, n.ReadAt is not null);
}

public record NotificationSummary(int Unread);

// Reads a user's in-app notifications. They are written by INotifier.
public class NotificationsManager
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public NotificationsManager(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // Newest first.
    public Task<PagedResponse<NotificationResponse>> ListAsync(Guid userId, int page, int pageSize) =>
        Paging.ToPageAsync(
            _db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id),
            page, pageSize, NotificationResponse.From);

    public async Task<NotificationSummary> SummaryAsync(Guid userId) =>
        new(await _db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null));

    // Safe to run twice: only unread ones change.
    public Task ReadAllAsync(Guid userId)
    {
        var now = _clock.UtcNow;
        return _db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now));
    }
}
