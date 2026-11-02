namespace Booking.Api.Domain;

// A category of resource. The name is data an admin enters, never a word in the code.
public class ResourceType
{
    public Guid Id { get; set; }

    // Unique ignoring case; the case the admin typed is kept for display.
    public string Name { get; set; } = "";
}
