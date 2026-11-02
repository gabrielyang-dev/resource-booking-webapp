using Booking.Api.Domain;
using Booking.Api.Rules;

namespace Booking.UnitTests;

// Sandwiches: 24 hours' notice, for bookings starting 08:00 to 15:00 London time.
// 10 June 2030 is in British Summer Time, so London = UTC + 1.
public class AddOnCheckTests
{
    private static readonly OrderedAddOn Sandwiches = new("Sandwiches", 24 * 60, new TimeOnly(8, 0), new TimeOnly(15, 0), 6);

    private static DateTime Utc(int day, int hour, int minute = 0) => new(2030, 6, day, hour, minute, 0, DateTimeKind.Utc);

    private static BookingCandidate Booking(DateTime start, DateTime now) =>
        new("Room A", 4, "Europe/London", Guid.NewGuid(), new TimeRange(start, start.AddHours(1)), null, now, [], [Sandwiches]);

    [Fact]
    public void Ordering_exactly_the_lead_time_ahead_is_allowed()
    {
        var start = Utc(10, 11); // 12:00 London
        Assert.Empty(new AddOnLeadTimeCheck().Check(Booking(start, now: start.AddHours(-24))));
    }

    [Fact]
    public void Ordering_a_minute_too_late_is_refused_with_the_deadline()
    {
        var start = Utc(10, 11);
        var violation = Assert.Single(new AddOnLeadTimeCheck().Check(Booking(start, now: start.AddHours(-24).AddMinutes(1))));
        Assert.Equal("ADD_ON_TOO_LATE", violation.Code);
        Assert.Contains("Sandwiches need 24 hours' notice", violation.Message);
        Assert.Contains("Sun 9 Jun 12:00", violation.Message);
    }

    [Fact]
    public void Starting_a_minute_before_the_add_on_closes_is_allowed()
    {
        // 13:59 UTC is 14:59 London; sandwiches stop at 15:00.
        Assert.Empty(new AddOnHoursCheck().Check(Booking(Utc(10, 13, 59), now: Utc(1, 0))));
    }

    [Fact]
    public void Starting_at_the_add_on_closing_time_is_refused()
    {
        var violation = Assert.Single(new AddOnHoursCheck().Check(Booking(Utc(10, 14), now: Utc(1, 0))));
        Assert.Equal("ADD_ON_UNAVAILABLE", violation.Code);
        Assert.Contains("08:00 to 15:00", violation.Message);
    }

    [Fact]
    public void A_booking_without_add_ons_passes_both_checks()
    {
        var booking = new BookingCandidate("Room A", 4, "Europe/London", Guid.NewGuid(), new TimeRange(Utc(10, 20), Utc(10, 21)), null, Utc(10, 19), []);
        Assert.Empty(new AddOnLeadTimeCheck().Check(booking));
        Assert.Empty(new AddOnHoursCheck().Check(booking));
    }
}
