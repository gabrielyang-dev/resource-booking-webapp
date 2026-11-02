namespace Booking.Api.Domain;

// The kinds of rule an admin can configure. Each kind has one class in Rules/.
public enum RuleKind
{
    OpeningHours,
    MaxDuration,
    BookingWindow,
    DailyQuota,
}
