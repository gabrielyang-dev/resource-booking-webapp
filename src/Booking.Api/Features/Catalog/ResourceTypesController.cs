using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Catalog;

[ApiController]
[Route("api/resource-types")]
[Authorize]
public class ResourceTypesController : ControllerBase
{
    private readonly ResourceTypesManager _types;

    public ResourceTypesController(ResourceTypesManager types)
    {
        _types = types;
    }

    [HttpGet]
    public async Task<PagedResponse<ResourceTypeResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _types.ListAsync(page, pageSize);

    [HttpGet("{id:guid}")]
    public async Task<ResourceTypeResponse> Get(Guid id) => await _types.GetAsync(id);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ResourceTypeRequest request)
    {
        var created = await _types.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ResourceTypeResponse> Update(Guid id, ResourceTypeRequest request) =>
        await _types.UpdateAsync(id, request);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _types.DeleteAsync(id);
        return NoContent();
    }
}
