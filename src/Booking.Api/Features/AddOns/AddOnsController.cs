using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.AddOns;

[ApiController]
[Route("api/add-ons")]
[Authorize]
public class AddOnsController : ControllerBase
{
    private readonly AddOnsManager _addOns;
    private readonly ICurrentUser _currentUser;

    public AddOnsController(AddOnsManager addOns, ICurrentUser currentUser)
    {
        _addOns = addOns;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<PagedResponse<AddOnResponse>> List(
        [FromQuery] Guid? locationId, [FromQuery] bool includeInactive = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _addOns.ListAsync(locationId, includeInactive && _currentUser.Role == Role.Admin, page, pageSize);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(AddOnRequest request) =>
        StatusCode(StatusCodes.Status201Created, await _addOns.CreateAsync(request));

    // Deactivate with active: false; add-ons are never deleted, so old orders keep them.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<AddOnResponse> Update(Guid id, AddOnRequest request) => await _addOns.UpdateAsync(id, request);
}
