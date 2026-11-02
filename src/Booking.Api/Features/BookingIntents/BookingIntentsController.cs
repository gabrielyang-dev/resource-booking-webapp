using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.BookingIntents;

[ApiController]
[Route("api/booking-intents")]
[Authorize]
public class BookingIntentsController : ControllerBase
{
    private readonly BookingIntentsManager _intents;

    public BookingIntentsController(BookingIntentsManager intents)
    {
        _intents = intents;
    }

    // Lets the UI hide the text box when no AI is configured.
    [HttpGet("enabled")]
    public BookingIntentStatus Enabled() => _intents.GetStatus();

    // Returns search fields for the sentence. Never creates a booking.
    [HttpPost]
    public async Task<BookingIntentResponse> Parse(BookingIntentRequest request, CancellationToken cancellationToken) =>
        await _intents.ParseAsync(request, cancellationToken);
}
