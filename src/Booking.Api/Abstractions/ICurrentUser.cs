using Booking.Api.Domain;

namespace Booking.Api.Abstractions;

// The signed-in user, read from the cookie claims.
public interface ICurrentUser
{
    bool IsSignedIn { get; }
    Guid Id { get; }
    string Name { get; }
    Role Role { get; }
}
