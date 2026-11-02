using Booking.Api.Abstractions;
using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Notifications;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationsManager _notifications;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(NotificationsManager notifications, ICurrentUser currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<PagedResponse<NotificationResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _notifications.ListAsync(_currentUser.Id, page, pageSize);

    // For the number on the bell.
    [HttpGet("summary")]
    public async Task<NotificationSummary> Summary() => await _notifications.SummaryAsync(_currentUser.Id);

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await _notifications.ReadAllAsync(_currentUser.Id);
        return NoContent();
    }
}
