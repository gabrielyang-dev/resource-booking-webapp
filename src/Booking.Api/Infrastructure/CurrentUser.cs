using System.Security.Claims;
using Booking.Api.Abstractions;
using Booking.Api.Domain;

namespace Booking.Api.Infrastructure;

public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User ?? new ClaimsPrincipal();
    }

    public bool IsSignedIn => _principal.Identity?.IsAuthenticated == true;

    public Guid Id => Guid.Parse(_principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public string Name => _principal.FindFirstValue(ClaimTypes.Name)!;

    public Role Role => Enum.Parse<Role>(_principal.FindFirstValue(ClaimTypes.Role)!);
}
