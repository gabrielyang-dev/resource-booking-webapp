using Booking.Api.Abstractions;
using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.BookingIntents;

public record BookingIntentRequest(string? Text, Guid? LocationId);

// The search fields only. Nothing is booked: the user checks them and searches.
public record BookingIntentResponse(
    Guid? ResourceTypeId, string? ResourceTypeName, string Date, string Start, string End, int? Attendees);

public record BookingIntentStatus(bool Enabled);

public class BookingIntentsManager
{
    private const int MaxLength = 300;

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IBookingIntentParser? _parser;
    private readonly ILogger<BookingIntentsManager> _logger;

    // The parser is only registered when an OpenAI key is configured; without one the feature is off.
    public BookingIntentsManager(AppDbContext db, IClock clock, ILogger<BookingIntentsManager> logger, IBookingIntentParser? parser = null)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
        _parser = parser;
    }

    public BookingIntentStatus GetStatus() => new(_parser is not null);

    public async Task<BookingIntentResponse> ParseAsync(BookingIntentRequest request, CancellationToken cancellationToken)
    {
        if (_parser is null)
        {
            throw ApiException.NotFound("Typing a sentence is switched off here. Fill in the search form instead.");
        }
        var text = request.Text?.Trim() ?? "";
        if (text.Length is 0 or > MaxLength)
        {
            throw ApiException.Invalid(new() { ["text"] = [$"Describe what you need in up to {MaxLength} characters, for example \"a room for 6 tomorrow at 2\"."] });
        }
        var location = await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken)
            ?? throw ApiException.Invalid(new() { ["locationId"] = ["Choose a location first."] });

        var zone = LocalTime.Zone(location.TimeZone);
        var types = await _db.ResourceTypes.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new ResourceTypeOption(t.Id, t.Name)).ToListAsync(cancellationToken);
        var context = new BookingIntentContext(DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow, zone)), location.TimeZone, types);

        BookingIntent? intent;
        try
        {
            intent = await _parser.ParseAsync(text, context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Booking intent parser failed.");
            intent = null;
        }
        if (intent is null)
        {
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "INTENT_NOT_UNDERSTOOD",
                "Sorry, I couldn't understand that. Fill in the form instead.");
        }

        return new BookingIntentResponse(intent.ResourceTypeId, types.FirstOrDefault(t => t.Id == intent.ResourceTypeId)?.Name,
            intent.Date.ToString("yyyy-MM-dd"), intent.Start.ToString("HH:mm"), intent.End.ToString("HH:mm"), intent.Attendees);
    }
}
