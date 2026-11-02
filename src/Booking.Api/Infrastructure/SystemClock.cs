using Booking.Api.Abstractions;

namespace Booking.Api.Infrastructure;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
