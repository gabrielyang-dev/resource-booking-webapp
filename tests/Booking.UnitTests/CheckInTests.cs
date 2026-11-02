using Booking.Api.Domain;
using Booking.Api.Features.Insights;
using Booking.Api.Rules;

namespace Booking.UnitTests;

public class CheckInTests
{
    private static DateTime At(int hour, int minute = 0) => new(2030, 6, 10, hour, minute, 0, DateTimeKind.Utc);

    private static readonly TimeRange Meeting = new(At(9), At(10));
    private static readonly CheckInSettings Required = new(Required: true, OpensMinutesBefore: 15, ReleaseAfterMinutes: 15);

    [Fact]
    public void Check_in_opens_exactly_the_set_minutes_before_the_start()
    {
        // [08:45, 09:15): 08:45 is inside, 08:44 is not.
        Assert.Equal(At(8, 45), CheckInRule.Window(Meeting, Required).Start);
    }

    [Fact]
    public void When_required_check_in_closes_at_the_release_time_otherwise_at_the_end()
    {
        Assert.Equal(At(9, 15), CheckInRule.Window(Meeting, Required).End);
        Assert.Equal(At(10), CheckInRule.Window(Meeting, Required with { Required = false }).End);
    }

    [Fact]
    public void A_release_is_never_later_than_the_end_of_a_short_booking()
    {
        var short10 = new TimeRange(At(9), At(9, 10));
        Assert.Equal(At(9, 10), CheckInRule.ReleaseAt(short10, Required));
    }

    [Fact]
    public void Invalid_check_in_settings_are_refused()
    {
        Assert.Null(new CheckInRule().ValidateSettings("""{ "required": true, "opensMinutesBefore": 15, "releaseAfterMinutes": 15 }"""));
        Assert.NotNull(new CheckInRule().ValidateSettings("""{ "required": true, "opensMinutesBefore": 15, "releaseAfterMinutes": 0 }"""));
    }

    [Fact]
    public void Checked_in_hours_count_only_bookings_someone_checked_in_to_and_no_shows_count_released_ones()
    {
        var day = new TimeRange(At(0), At(0).AddDays(1));
        var bookings = new (TimeRange, BookingStatus, DateTime?)[]
        {
            (new TimeRange(At(9), At(11)), BookingStatus.Completed, At(9, 3)),   // used: 2 h
            (new TimeRange(At(12), At(13)), BookingStatus.Completed, null),      // held, never checked in
            (new TimeRange(At(14), At(15)), BookingStatus.Released, null),       // no-show, released
            (new TimeRange(At(16), At(17)), BookingStatus.CheckedIn, At(16)),    // in the room now: 1 h
        };

        Assert.Equal(3, UtilisationCalculator.CheckedInHours(bookings, day));
        Assert.Equal(1, UtilisationCalculator.NoShowHours(bookings, day));
        Assert.Equal(4, UtilisationCalculator.BookedHours(bookings.Select(b => (b.Item1, b.Item2)), day));
    }
}
