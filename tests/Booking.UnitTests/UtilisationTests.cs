using Booking.Api.Domain;
using Booking.Api.Features.Insights;
using Booking.Api.Rules;

namespace Booking.UnitTests;

public class UtilisationTests
{
    private static readonly TimeZoneInfo London = LocalTime.Zone("Europe/London");

    private static DateTime Utc(int month, int day, int hour) => new(2030, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static readonly TimeRange Monday = new(Utc(6, 10, 0), Utc(6, 11, 0));

    [Fact]
    public void A_booking_that_starts_before_the_period_counts_only_its_hours_inside()
    {
        var bookings = new[] { (new TimeRange(Utc(6, 9, 22), Utc(6, 10, 3)), BookingStatus.Completed) };
        Assert.Equal(3, UtilisationCalculator.BookedHours(bookings, Monday));
    }

    [Fact]
    public void A_booking_that_ends_after_the_period_counts_only_its_hours_inside()
    {
        var bookings = new[] { (new TimeRange(Utc(6, 10, 22), Utc(6, 11, 4)), BookingStatus.Confirmed) };
        Assert.Equal(2, UtilisationCalculator.BookedHours(bookings, Monday));
    }

    [Fact]
    public void Pending_and_cancelled_bookings_are_not_counted()
    {
        var bookings = new[]
        {
            (new TimeRange(Utc(6, 10, 9), Utc(6, 10, 10)), BookingStatus.Pending),
            (new TimeRange(Utc(6, 10, 10), Utc(6, 10, 11)), BookingStatus.Cancelled),
            (new TimeRange(Utc(6, 10, 11), Utc(6, 10, 12)), BookingStatus.CheckedIn),
        };
        Assert.Equal(1, UtilisationCalculator.BookedHours(bookings, Monday));
    }

    [Fact]
    public void A_24_hour_resource_is_open_23_hours_when_the_clocks_go_forward_and_25_when_they_go_back()
    {
        var forward = new DateOnly(2026, 3, 29);
        var back = new DateOnly(2026, 10, 25);

        Assert.Equal(23, UtilisationCalculator.OpenHours(forward, forward, London, null));
        Assert.Equal(25, UtilisationCalculator.OpenHours(back, back, London, null));
    }

    [Fact]
    public void An_0800_to_1800_resource_stays_open_10_hours_on_clock_change_days()
    {
        var everyDay = new OpeningHoursSettings("08:00", "18:00", RuleSettings.AllDays);
        var forward = new DateOnly(2026, 3, 29);
        var back = new DateOnly(2026, 10, 25);

        Assert.Equal(10, UtilisationCalculator.OpenHours(forward, forward, London, everyDay));
        Assert.Equal(10, UtilisationCalculator.OpenHours(back, back, London, everyDay));
    }

    [Fact]
    public void Closed_days_add_no_open_hours_and_zero_open_hours_means_zero_utilisation()
    {
        var weekdays = new OpeningHoursSettings("08:00", "18:00", ["Mon", "Tue", "Wed", "Thu", "Fri"]);
        var monday = new DateOnly(2030, 6, 10);

        Assert.Equal(50, UtilisationCalculator.OpenHours(monday, monday.AddDays(6), London, weekdays));
        Assert.Equal(0, UtilisationCalculator.OpenHours(monday.AddDays(5), monday.AddDays(6), London, weekdays));
        Assert.Equal(0, UtilisationCalculator.Ratio(5, 0));
        Assert.Equal(0.2, UtilisationCalculator.Ratio(10, 50));
    }
}
