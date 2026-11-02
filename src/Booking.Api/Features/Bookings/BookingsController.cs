using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Features.Catalog;
using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Bookings;

[ApiController]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly BookingManager _bookings;
    private readonly ICurrentUser _currentUser;

    public BookingsController(BookingManager bookings, ICurrentUser currentUser)
    {
        _bookings = bookings;
        _currentUser = currentUser;
    }

    // 201 for a new booking, 200 when the Idempotency-Key was already used by this user.
    [HttpPost("api/bookings")]
    public async Task<IActionResult> Create(
        BookingRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var result = await _bookings.CreateAsync(_currentUser.Id, IsAdmin, request, idempotencyKey);
        return StatusCode(result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Booking);
    }

    // Free resources that pass every rule for the signed-in user at that time.
    [HttpGet("api/availability")]
    public async Task<PagedResponse<ResourceResponse>> Availability(
        [FromQuery] Guid? locationId, [FromQuery] Guid? resourceTypeId, [FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end,
        [FromQuery] int? attendees, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _bookings.FindAvailableAsync(_currentUser.Id, locationId, resourceTypeId, start, end, attendees, page, pageSize);

    [HttpGet("api/bookings/{id:guid}")]
    public async Task<BookingResponse> Get(Guid id) =>
        await _bookings.GetForUserAsync(id, _currentUser.Id, IsAdmin);

    [HttpPut("api/bookings/{id:guid}")]
    public async Task<BookingResponse> Reschedule(Guid id, RescheduleRequest request) =>
        await _bookings.RescheduleAsync(id, _currentUser.Id, IsAdmin, request);

    [HttpPost("api/bookings/{id:guid}/check-in")]
    public async Task<BookingResponse> CheckIn(Guid id) =>
        await _bookings.CheckInAsync(id, _currentUser.Id, IsAdmin);

    [HttpPost("api/bookings/{id:guid}/cancel")]
    public async Task<BookingResponse> Cancel(Guid id) =>
        await _bookings.CancelAsync(id, _currentUser.Id, IsAdmin);

    private bool IsAdmin => _currentUser.Role == Role.Admin;

    [HttpGet("api/bookings")]
    public async Task<PagedResponse<BookingResponse>> ListMine(
        [FromQuery] bool mine = true, [FromQuery] string? when = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _bookings.ListMineAsync(_currentUser.Id, mine, when, page, pageSize);

    [HttpGet("api/resources/{resourceId:guid}/bookings")]
    public async Task<IReadOnlyList<ResourceBookingResponse>> ListForResource(
        Guid resourceId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to) =>
        await _bookings.ListForResourceAsync(resourceId, from, to);
}
