using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Catalog;

[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly ResourcesManager _resources;
    private readonly ICurrentUser _currentUser;

    public ResourcesController(ResourcesManager resources, ICurrentUser currentUser)
    {
        _resources = resources;
        _currentUser = currentUser;
    }

    // Everyone sees active resources; only an admin can add includeInactive=true to see the rest.
    [HttpGet]
    public async Task<PagedResponse<ResourceResponse>> List(
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? resourceTypeId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        await _resources.ListAsync(
            locationId, resourceTypeId, includeInactive && _currentUser.Role == Role.Admin, page, pageSize);

    [HttpGet("{id:guid}")]
    public async Task<ResourceResponse> Get(Guid id) =>
        await _resources.GetAsync(id, _currentUser.Role == Role.Admin);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ResourceRequest request)
    {
        var created = await _resources.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ResourceResponse> Update(Guid id, ResourceRequest request) =>
        await _resources.UpdateAsync(id, request);
}
