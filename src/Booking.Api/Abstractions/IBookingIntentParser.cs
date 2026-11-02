namespace Booking.Api.Abstractions;

// Turns one sentence ("a room for 6 tomorrow at 2 for an hour") into booking search fields.
// AI interprets, never decides: the result only fills a search form the user checks;
// the rules and the database still decide whether a booking is allowed.
// Two implementations: OpenAI in the app, a fake in the tests.
public interface IBookingIntentParser
{
    // Null when the sentence can't be understood.
    Task<BookingIntent?> ParseAsync(string text, BookingIntentContext context, CancellationToken cancellationToken);
}

// Times are local to the location.
public record BookingIntent(Guid? ResourceTypeId, DateOnly Date, TimeOnly Start, TimeOnly End, int? Attendees);

// What the parser needs to know: today's local date, the location's zone, and the real resource types to pick from.
public record BookingIntentContext(DateOnly Today, string TimeZone, IReadOnlyList<ResourceTypeOption> ResourceTypes);

public record ResourceTypeOption(Guid Id, string Name);
