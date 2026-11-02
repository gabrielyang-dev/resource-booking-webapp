using System.Globalization;
using System.Text.Json;

namespace Booking.Api.Rules;

// The JSON shape of each rule kind's settings. Times are local "HH:mm"; days are "Mon" to "Sun".
public record OpeningHoursSettings(string Opens, string Closes, string[] Days);
public record MaxDurationSettings(int MaxMinutes);
public record BookingWindowSettings(int MinNoticeMinutes, int MaxDaysAhead);
public record DailyQuotaSettings(int MaxMinutesPerDay);

public static class RuleSettings
{
    public static readonly string[] AllDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Null when the JSON does not fit the record.
    public static T? Parse<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string ToJson<T>(T settings) => JsonSerializer.Serialize(settings, Json);

    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    // "Mon" for Monday, matching the stored day names.
    public static string DayName(DayOfWeek day) => AllDays[((int)day + 6) % 7];

    // "2 hours 30 minutes", "1 hour", "45 minutes".
    public static string Duration(TimeSpan span)
    {
        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;
        var parts = new List<string>();
        if (hours > 0)
        {
            parts.Add(hours == 1 ? "1 hour" : $"{hours} hours");
        }
        if (minutes > 0 || hours == 0)
        {
            parts.Add(minutes == 1 ? "1 minute" : $"{minutes} minutes");
        }
        return string.Join(" ", parts);
    }
}
