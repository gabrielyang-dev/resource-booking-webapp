namespace Booking.Api.Domain;

public enum Role
{
    Admin,
    Member,
}

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    // Always stored trimmed and in lower case, so the unique index is case-insensitive.
    public string Email { get; set; } = "";

    public Role Role { get; set; }
}
