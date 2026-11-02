using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Abstractions;

namespace Booking.Api.Infrastructure;

// Asks OpenAI's chat completions API for the search fields as strict JSON, then validates the reply.
// Model and key come from configuration (OpenAI:Model, OpenAI:ApiKey); the key is never committed.
public class OpenAiBookingIntentParser : IBookingIntentParser
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OpenAiBookingIntentParser(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _model = configuration["OpenAI:Model"] ?? "gpt-6-luna";
        _http.BaseAddress = new Uri("https://api.openai.com/");
        _http.DefaultRequestHeaders.Authorization = new("Bearer", configuration["OpenAI:ApiKey"]);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<BookingIntent?> ParseAsync(string text, BookingIntentContext context, CancellationToken cancellationToken)
    {
        var types = string.Join("\n", context.ResourceTypes.Select(t => $"- {t.Id}: {t.Name}"));
        var instructions =
            $"You turn one sentence into fields for a booking search. Today is {context.Today:yyyy-MM-dd} ({context.Today:dddd}), " +
            $"time zone {context.TimeZone}. Resource types (use the id, or null if unclear):\n{types}\n" +
            "Times are 24-hour HH:mm local times. If no end or length is given, assume one hour. " +
            "If no day is given, use today. Attendees is null unless a number of people is given.";

        var request = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = instructions },
                new { role = "user", content = text },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = "booking_search", strict = true, schema = BookingIntentJson.Schema(context.ResourceTypes) },
            },
        };

        using var response = await _http.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var content = body.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content is null ? null : BookingIntentJson.Validate(content, context);
    }
}
