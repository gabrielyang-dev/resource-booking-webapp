namespace Booking.Api.Domain;

// A half-open range [Start, End), both in UTC.
public record TimeRange(DateTime Start, DateTime End)
{
    // Ending at 10:00 and starting at 10:00 is not an overlap.
    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;
}
