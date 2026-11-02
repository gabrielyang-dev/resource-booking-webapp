using Booking.Api.Abstractions;

namespace Booking.IntegrationTests;

// Stands in for OpenAI. "nonsense" is not understood, "boom" throws, anything else is
// "a meeting room for 6 tomorrow 14:00 to 15:00".
public class FakeBookingIntentParser : IBookingIntentParser
{
    public Task<BookingIntent?> ParseAsync(string text, BookingIntentContext context, CancellationToken cancellationToken)
    {
        if (text.Contains("boom"))
        {
            throw new HttpRequestException("The AI service is down.");
        }
        if (text.Contains("nonsense"))
        {
            return Task.FromResult<BookingIntent?>(null);
        }
        var room = context.ResourceTypes.First(t => t.Name == "Meeting room");
        return Task.FromResult<BookingIntent?>(
            new BookingIntent(room.Id, context.Today.AddDays(1), new TimeOnly(14, 0), new TimeOnly(15, 0), 6));
    }
}
