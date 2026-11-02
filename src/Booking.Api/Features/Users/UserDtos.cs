using Booking.Api.Domain;

namespace Booking.Api.Features.Users;

// Role arrives as text ("Admin" or "Member") so a typo is a field error, not a binding failure.
public record UserRequest(string? Name, string? Email, string? Role);

public record UserResponse(Guid Id, string Name, string Email, string Role)
{
    public static UserResponse From(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
}
