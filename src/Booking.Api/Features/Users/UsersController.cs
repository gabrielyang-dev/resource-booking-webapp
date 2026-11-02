using Booking.Api.Features.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Users;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UsersManager _users;

    public UsersController(UsersManager users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<PagedResponse<UserResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _users.ListAsync(page, pageSize);

    [HttpGet("{id:guid}")]
    public async Task<UserResponse> Get(Guid id) => await _users.GetAsync(id);

    [HttpPost]
    public async Task<IActionResult> Create(UserRequest request)
    {
        var created = await _users.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<UserResponse> Update(Guid id, UserRequest request) => await _users.UpdateAsync(id, request);
}
