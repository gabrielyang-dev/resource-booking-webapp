using Booking.Api.Domain;

namespace Booking.UnitTests;

public class TimeRangeTests
{
    private static TimeRange Range(int startHour, int endHour) =>
        new(new DateTime(2030, 6, 10, startHour, 0, 0, DateTimeKind.Utc), new DateTime(2030, 6, 10, endHour, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Partly_overlapping_ranges_overlap_both_ways()
    {
        Assert.True(Range(10, 12).Overlaps(Range(11, 13)));
        Assert.True(Range(11, 13).Overlaps(Range(10, 12)));
    }

    [Fact]
    public void Back_to_back_ranges_do_not_overlap()
    {
        Assert.False(Range(10, 11).Overlaps(Range(11, 12)));
        Assert.False(Range(11, 12).Overlaps(Range(10, 11)));
    }

    [Fact]
    public void A_range_inside_another_overlaps_it()
    {
        Assert.True(Range(9, 17).Overlaps(Range(11, 12)));
        Assert.True(Range(11, 12).Overlaps(Range(9, 17)));
    }
}
