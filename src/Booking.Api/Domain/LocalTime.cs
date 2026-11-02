namespace Booking.Api.Domain;

// Converts between UTC (how we store times) and a location's local clock (how rules judge them).
public static class LocalTime
{
    public static TimeZoneInfo Zone(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

    // The UTC instant of a local wall-clock time. On the day the clocks change this still gives the right instant.
    public static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        // A wall-clock time skipped by the clocks going forward does not exist: move it on an hour.
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    // The UTC range [start, end) of one local day: 23 or 25 hours long on clock-change days.
    public static TimeRange Day(DateOnly date, TimeZoneInfo zone) =>
        new(ToUtc(date, TimeOnly.MinValue, zone), ToUtc(date.AddDays(1), TimeOnly.MinValue, zone));
}
