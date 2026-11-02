using System.Globalization;
using System.Text.Json;
using Booking.Api.Abstractions;

namespace Booking.Api.Infrastructure;

// The JSON the model must return, and the check that what came back makes sense.
// Pure (no network), so it is unit-tested with canned replies.
public static class BookingIntentJson
{
    // Strict JSON schema sent to OpenAI: every field required, null allowed where we can't be sure.
    public static object Schema(IReadOnlyList<ResourceTypeOption> types) => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "resourceTypeId", "date", "start", "end", "attendees" },
        properties = new
        {
            resourceTypeId = new { type = new[] { "string", "null" }, @enum = types.Select(t => (string?)t.Id.ToString()).Append(null).ToArray() },
            date = new { type = "string", description = "yyyy-MM-dd" },
            start = new { type = "string", description = "HH:mm, 24-hour, local time" },
            end = new { type = "string", description = "HH:mm, 24-hour, local time" },
            attendees = new { type = new[] { "integer", "null" } },
        },
    };

    // Null unless the reply has the right shape and sensible values: a known type id (or none),
    // a real date from today to a year ahead, start before end, and 1 to 1,000 attendees (or none).
    public static BookingIntent? Validate(string json, BookingIntentContext context)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Guid? typeId = null;
            var typeText = root.GetProperty("resourceTypeId");
            if (typeText.ValueKind == JsonValueKind.String)
            {
                if (!Guid.TryParse(typeText.GetString(), out var parsed) || context.ResourceTypes.All(t => t.Id != parsed))
                {
                    return null;
                }
                typeId = parsed;
            }

            if (!DateOnly.TryParseExact(root.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date < context.Today || date > context.Today.AddDays(366))
            {
                return null;
            }
            if (!TimeOnly.TryParseExact(root.GetProperty("start").GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                || !TimeOnly.TryParseExact(root.GetProperty("end").GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
                || end <= start)
            {
                return null;
            }

            int? attendees = null;
            var attendeesValue = root.GetProperty("attendees");
            if (attendeesValue.ValueKind == JsonValueKind.Number)
            {
                if (!attendeesValue.TryGetInt32(out var n) || n is < 1 or > 1000)
                {
                    return null;
                }
                attendees = n;
            }

            return new BookingIntent(typeId, date, start, end, attendees);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
