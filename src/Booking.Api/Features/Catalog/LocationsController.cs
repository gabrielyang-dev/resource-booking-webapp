using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Catalog;

[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly LocationsManager _locations;

    public LocationsController(LocationsManager locations)
    {
        _locations = locations;
    }

    [HttpGet]
    public async Task<PagedResponse<LocationResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _locations.ListAsync(page, pageSize);

    [HttpGet("{id:guid}")]
    public async Task<LocationResponse> Get(Guid id) => await _locations.GetAsync(id);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(LocationRequest request)
    {
        var created = await _locations.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<LocationResponse> Update(Guid id, LocationRequest request) =>
        await _locations.UpdateAsync(id, request);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _locations.DeleteAsync(id);
        return NoContent();
    }
}
