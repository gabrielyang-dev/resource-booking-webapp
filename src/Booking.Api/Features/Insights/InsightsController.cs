using Booking.Api.Features.Bookings;
using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Insights;

[ApiController]
[Route("api/insights")]
[Authorize(Roles = "Admin")]
public class InsightsController : ControllerBase
{
    private readonly InsightsManager _insights;

    public InsightsController(InsightsManager insights)
    {
        _insights = insights;
    }

    // from and to are local dates (yyyy-MM-dd); to is included. Defaults to this week.
    [HttpGet("utilisation")]
    public async Task<UtilisationResponse> Utilisation(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? locationId, [FromQuery] Guid? resourceTypeId) =>
        await _insights.GetUtilisationAsync(from, to, locationId, resourceTypeId);

    [HttpGet("history")]
    public async Task<PagedResponse<BookingResponse>> History(
        [FromQuery] Guid? resourceId, [FromQuery] Guid? userId, [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _insights.GetHistoryAsync(resourceId, userId, status, from, to, page, pageSize);
}
