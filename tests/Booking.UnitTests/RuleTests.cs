using Booking.Api.Domain;
using Booking.Api.Rules;

namespace Booking.UnitTests;

// Two tests per rule: just inside the limit and just outside. Times are UTC; the resource is in London.
// 10 June 2030 is a Monday in summer (London = UTC+1); 7 January 2030 is a Monday in winter (London = UTC).
public class RuleTests
{
    private static readonly DateTime Now = new(2030, 6, 3, 8, 0, 0, DateTimeKind.Utc);

    private static DateTime Utc(int month, int day, int hour, int minute = 0, int second = 0) =>
        new(2030, month, day, hour, minute, second, DateTimeKind.Utc);

    private static BookingCandidate Booking(DateTime start, DateTime end, int? attendees = null, int? capacity = 4,
        DateTime? now = null, List<TimeRange>? others = null) =>
        new("Room A", capacity, "Europe/London", Guid.NewGuid(), new TimeRange(start, end), attendees, now ?? Now, others ?? []);

    private static string Codes(IEnumerable<Violation> violations) => string.Join(",", violations.Select(v => v.Code));

    // --- Valid time range ---

    [Fact]
    public void Starting_exactly_now_is_allowed()
    {
        Assert.Empty(new ValidTimeRangeCheck().Check(Booking(Now, Now.AddHours(1))));
    }

    [Fact]
    public void Starting_a_minute_ago_is_in_the_past()
    {
        Assert.Equal("IN_THE_PAST", Codes(new ValidTimeRangeCheck().Check(Booking(Now.AddMinutes(-1), Now.AddHours(1)))));
    }

    [Fact]
    public void Ending_one_minute_after_the_start_is_allowed()
    {
        var start = Utc(6, 10, 9);
        Assert.Empty(new ValidTimeRangeCheck().Check(Booking(start, start.AddMinutes(1))));
    }

    [Fact]
    public void Ending_at_the_start_is_not_allowed()
    {
        var start = Utc(6, 10, 9);
        Assert.Equal("END_BEFORE_START", Codes(new ValidTimeRangeCheck().Check(Booking(start, start))));
    }

    [Fact]
    public void Whole_minutes_are_allowed_but_a_stray_second_is_not()
    {
        Assert.Empty(new ValidTimeRangeCheck().Check(Booking(Utc(6, 10, 9, 15), Utc(6, 10, 10))));
        Assert.Equal("NOT_WHOLE_MINUTES", Codes(new ValidTimeRangeCheck().Check(Booking(Utc(6, 10, 9, 15, 1), Utc(6, 10, 10)))));
    }

    // --- Capacity ---

    [Fact]
    public void Attendees_equal_to_capacity_fit()
    {
        Assert.Empty(new CapacityCheck().Check(Booking(Utc(6, 10, 9), Utc(6, 10, 10), attendees: 4, capacity: 4)));
    }

    [Fact]
    public void One_attendee_over_capacity_does_not_fit()
    {
        var violation = Assert.Single(new CapacityCheck().Check(Booking(Utc(6, 10, 9), Utc(6, 10, 10), attendees: 5, capacity: 4)));
        Assert.Equal("OVER_CAPACITY", violation.Code);
        Assert.Contains("holds 4 people", violation.Message);
    }

    [Fact]
    public void No_capacity_means_no_people_limit()
    {
        Assert.Empty(new CapacityCheck().Check(Booking(Utc(6, 10, 9), Utc(6, 10, 10), attendees: 500, capacity: null)));
    }

    // --- Opening hours (08:00 to 18:00, Monday to Friday, London time) ---

    private const string OfficeHours = """{ "opens": "08:00", "closes": "18:00", "days": ["Mon","Tue","Wed","Thu","Fri"] }""";

    [Fact]
    public void In_summer_0730_utc_is_0830_in_london_and_inside_opening_hours()
    {
        Assert.Empty(new OpeningHoursRule().Check(Booking(Utc(6, 10, 7, 30), Utc(6, 10, 8, 30)), OfficeHours));
    }

    [Fact]
    public void In_winter_0730_utc_is_0730_in_london_and_before_opening()
    {
        Assert.Equal("OUTSIDE_OPENING_HOURS", Codes(new OpeningHoursRule().Check(Booking(Utc(1, 7, 7, 30), Utc(1, 7, 8, 30)), OfficeHours)));
    }

    [Fact]
    public void Ending_exactly_at_closing_time_is_allowed()
    {
        // 17:00 UTC is 18:00 in London in summer.
        Assert.Empty(new OpeningHoursRule().Check(Booking(Utc(6, 10, 16), Utc(6, 10, 17)), OfficeHours));
    }

    [Fact]
    public void Ending_a_minute_after_closing_time_is_not_allowed()
    {
        Assert.Equal("OUTSIDE_OPENING_HOURS", Codes(new OpeningHoursRule().Check(Booking(Utc(6, 10, 16), Utc(6, 10, 17, 1)), OfficeHours)));
    }

    [Fact]
    public void A_saturday_is_a_closed_day()
    {
        Assert.Equal("CLOSED_DAY", Codes(new OpeningHoursRule().Check(Booking(Utc(6, 8, 9), Utc(6, 8, 10)), OfficeHours)));
    }

    [Fact]
    public void Closing_before_opening_is_invalid_settings()
    {
        Assert.NotNull(new OpeningHoursRule().ValidateSettings("""{ "opens": "18:00", "closes": "08:00", "days": ["Mon"] }"""));
        Assert.Null(new OpeningHoursRule().ValidateSettings(OfficeHours));
    }

    // --- Max duration (2 hours) ---

    [Fact]
    public void Exactly_the_maximum_duration_is_allowed()
    {
        Assert.Empty(new MaxDurationRule().Check(Booking(Utc(6, 10, 9), Utc(6, 10, 11)), """{ "maxMinutes": 120 }"""));
    }

    [Fact]
    public void One_minute_over_the_maximum_duration_is_too_long()
    {
        var violation = Assert.Single(new MaxDurationRule().Check(Booking(Utc(6, 10, 9), Utc(6, 10, 11, 1)), """{ "maxMinutes": 120 }"""));
        Assert.Equal("TOO_LONG", violation.Code);
        Assert.Contains("at most 2 hours", violation.Message);
    }

    // --- Booking window (60 minutes' notice, 30 days ahead) ---

    private const string Window = """{ "minNoticeMinutes": 60, "maxDaysAhead": 30 }""";

    [Fact]
    public void Exactly_the_minimum_notice_is_enough()
    {
        Assert.Empty(new BookingWindowRule().Check(Booking(Now.AddMinutes(60), Now.AddMinutes(120)), Window));
    }

    [Fact]
    public void One_minute_short_of_the_notice_is_too_little()
    {
        Assert.Equal("TOO_LITTLE_NOTICE", Codes(new BookingWindowRule().Check(Booking(Now.AddMinutes(59), Now.AddMinutes(120)), Window)));
    }

    [Fact]
    public void Exactly_the_maximum_days_ahead_is_allowed_but_a_minute_more_is_not()
    {
        var limit = Now.AddDays(30);
        Assert.Empty(new BookingWindowRule().Check(Booking(limit, limit.AddHours(1)), Window));
        Assert.Equal("TOO_FAR_AHEAD", Codes(new BookingWindowRule().Check(Booking(limit.AddMinutes(1), limit.AddHours(1)), Window)));
    }

    // --- Daily quota (8 hours per owner per local day) ---

    private const string Quota = """{ "maxMinutesPerDay": 480 }""";

    [Fact]
    public void Reaching_the_daily_quota_exactly_is_allowed()
    {
        var others = new List<TimeRange> { new(Utc(6, 10, 7), Utc(6, 10, 14)) };
        Assert.Empty(new DailyQuotaRule().Check(Booking(Utc(6, 10, 14), Utc(6, 10, 15), others: others), Quota));
    }

    [Fact]
    public void One_minute_over_the_daily_quota_is_refused()
    {
        var others = new List<TimeRange> { new(Utc(6, 10, 7), Utc(6, 10, 14)) };
        Assert.Equal("DAILY_QUOTA_EXCEEDED", Codes(new DailyQuotaRule().Check(Booking(Utc(6, 10, 14), Utc(6, 10, 15, 1), others: others), Quota)));
    }

    [Fact]
    public void Bookings_on_another_local_day_do_not_count_towards_the_quota()
    {
        var others = new List<TimeRange> { new(Utc(6, 11, 7), Utc(6, 11, 15)) };
        Assert.Empty(new DailyQuotaRule().Check(Booking(Utc(6, 10, 9), Utc(6, 10, 17), others: others), Quota));
    }

    // --- Engine ---

    [Fact]
    public void The_engine_returns_every_violation_at_once_and_only_runs_configured_rules()
    {
        var engine = new RuleEngine(
            [new ValidTimeRangeCheck(), new CapacityCheck()],
            [new MaxDurationRule(), new OpeningHoursRule()]);
        var booking = Booking(Now.AddDays(-1), Now.AddDays(-1).AddHours(3), attendees: 6, capacity: 4);

        var violations = engine.Evaluate(booking, new Dictionary<RuleKind, string> { [RuleKind.MaxDuration] = """{ "maxMinutes": 120 }""" });

        Assert.Equal("IN_THE_PAST,OVER_CAPACITY,TOO_LONG", Codes(violations));
    }
}
