using Booking.Api.Domain;

namespace Booking.Api.Features.Session;

public record SignInRequest(Guid UserId);

// What the sign-in picker needs: no email.
public record PickerUser(Guid Id, string Name, string Role)
{
    public static PickerUser From(User user) => new(user.Id, user.Name, user.Role.ToString());
}
