namespace Booking.Api.Domain;

public class Location
{
    public Guid Id { get; set; }

    // Unique ignoring case; the case the admin typed is kept for display.
    public string Name { get; set; } = "";

    // An IANA id such as "Europe/London". Opening hours and quotas are judged in this zone.
    public string TimeZone { get; set; } = "";
}
