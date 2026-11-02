using Booking.Api.Domain;
using Booking.Api.Features.Bookings;

namespace Booking.UnitTests;

public class AlternativeTimesTests
{
    private static DateTime At(int hour, int minute = 0) => new(2030, 6, 10, hour, minute, 0, DateTimeKind.Utc);

    private static readonly TimeRange Day = new(At(0), At(0).AddDays(1));
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    [Fact]
    public void The_nearest_free_times_come_first_with_ties_going_to_the_earlier_time()
    {
        var taken = new TimeRange(At(10), At(11));
        var requested = new TimeRange(At(10), At(11));

        var found = AlternativeTimes.Find(requested, Day, Step, 3, c => !c.Overlaps(taken));

        Assert.Equal([At(9), At(11), At(8, 45)], found.Select(f => f.Start));
        Assert.All(found, f => Assert.Equal(TimeSpan.FromHours(1), f.End - f.Start));
    }

    [Fact]
    public void Only_times_inside_the_day_that_pass_the_check_are_returned_up_to_the_maximum()
    {
        var requested = new TimeRange(At(22), At(23, 30));

        // Only times starting at 21:00 or later pass (an "opening hours" stand-in).
        var found = AlternativeTimes.Find(requested, Day, Step, 3, c => c.Start >= At(21));

        Assert.Equal(3, found.Count);
        Assert.All(found, f => Assert.True(f.End <= Day.End && f.Start >= At(21)));
        Assert.Empty(AlternativeTimes.Find(requested, Day, Step, 3, _ => false));
    }
}
