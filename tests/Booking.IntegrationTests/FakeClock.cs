using Booking.Api.Abstractions;

namespace Booking.IntegrationTests;

// A clock the tests control. Starts on Monday 3 June 2030, 08:00 UTC, so bookings a week later
// are in the future and inside the seeded booking windows.
public class FakeClock : IClock
{
    public static readonly DateTime Start = new(2030, 6, 3, 8, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; } = Start;
}
