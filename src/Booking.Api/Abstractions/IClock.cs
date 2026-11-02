namespace Booking.Api.Abstractions;

// The only place logic gets "now" from, so tests can fix the time.
public interface IClock
{
    DateTime UtcNow { get; }
}
