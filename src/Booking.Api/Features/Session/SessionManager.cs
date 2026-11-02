using System.Security.Claims;
using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.Session;

public class SessionManager
{
    private readonly AppDbContext _db;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(AppDbContext db, ILogger<SessionManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<PagedResponse<PickerUser>> ListUsersAsync(int page, int pageSize)
    {
        var ordered = _db.Users.AsNoTracking().OrderBy(u => u.Name).ThenBy(u => u.Id);
        return Paging.ToPageAsync(ordered, page, pageSize, PickerUser.From);
    }

    public async Task<User> FindUserForSignInAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        return user ?? throw ApiException.NotFound("There is no user with that id. Pick a user from the list.");
    }

    // The cookie principal: id, name and role claims.
    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    // False when the cookie's user is gone or has a different role now.
    public async Task<bool> IsSessionValidAsync(ClaimsPrincipal? principal)
    {
        var idText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleText = principal?.FindFirstValue(ClaimTypes.Role);
        if (!Guid.TryParse(idText, out var id))
        {
            return false;
        }

        try
        {
            var storedRole = await _db.Users.Where(u => u.Id == id).Select(u => u.Role.ToString()).FirstOrDefaultAsync();
            return storedRole is not null && storedRole == roleText;
        }
        catch (Exception ex)
        {
            // Database trouble is not a reason to sign anyone out: keep the session and let the
            // endpoint report the problem itself (for example /api/health answering 503).
            _logger.LogWarning(ex, "Could not check the session against the database; keeping the session.");
            return true;
        }
    }
}
