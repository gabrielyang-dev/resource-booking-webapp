using Booking.Api.Abstractions;
using Booking.Api.Features.Shared;
using Booking.Api.Features.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Session;

[ApiController]
public class SessionController : ControllerBase
{
    private readonly SessionManager _session;
    private readonly UsersManager _users;
    private readonly ICurrentUser _currentUser;

    public SessionController(SessionManager session, UsersManager users, ICurrentUser currentUser)
    {
        _session = session;
        _users = users;
        _currentUser = currentUser;
    }

    [HttpGet("api/session/users")]
    [AllowAnonymous]
    public async Task<PagedResponse<PickerUser>> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await _session.ListUsersAsync(page, pageSize);

    [HttpPost("api/session")]
    [AllowAnonymous]
    public async Task<UserResponse> SignIn(SignInRequest request)
    {
        var user = await _session.FindUserForSignInAsync(request.UserId);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, SessionManager.CreatePrincipal(user));
        return UserResponse.From(user);
    }

    [HttpDelete("api/session")]
    [AllowAnonymous]
    public async Task<IActionResult> SignOutUser()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("api/me")]
    [Authorize]
    public async Task<UserResponse> Me() => await _users.GetAsync(_currentUser.Id);
}
