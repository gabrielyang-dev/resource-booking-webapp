using Booking.Api.Abstractions;
using Booking.Api.Infrastructure;

namespace Booking.UnitTests;

// Canned model replies: no network.
public class BookingIntentJsonTests
{
    private static readonly Guid RoomType = Guid.NewGuid();

    private static readonly BookingIntentContext Context =
        new(new DateOnly(2030, 6, 3), "Europe/London", [new ResourceTypeOption(RoomType, "Meeting room")]);

    private static string Reply(string type, string date = "2030-06-04", string start = "14:00", string end = "15:00", string attendees = "6") =>
        $$"""{ "resourceTypeId": {{type}}, "date": "{{date}}", "start": "{{start}}", "end": "{{end}}", "attendees": {{attendees}} }""";

    [Fact]
    public void A_good_reply_becomes_search_fields()
    {
        var intent = BookingIntentJson.Validate(Reply($"\"{RoomType}\""), Context);

        Assert.Equal(new BookingIntent(RoomType, new DateOnly(2030, 6, 4), new TimeOnly(14, 0), new TimeOnly(15, 0), 6), intent);
    }

    [Fact]
    public void No_type_and_no_attendees_are_allowed()
    {
        var intent = BookingIntentJson.Validate(Reply("null", attendees: "null"), Context);

        Assert.NotNull(intent);
        Assert.Null(intent.ResourceTypeId);
        Assert.Null(intent.Attendees);
    }

    [Fact]
    public void An_unknown_type_id_is_rejected()
    {
        Assert.Null(BookingIntentJson.Validate(Reply($"\"{Guid.NewGuid()}\""), Context));
    }

    [Fact]
    public void An_end_before_the_start_is_rejected()
    {
        Assert.Null(BookingIntentJson.Validate(Reply("null", start: "15:00", end: "14:00"), Context));
    }

    [Fact]
    public void A_date_in_the_past_or_bad_json_is_rejected()
    {
        Assert.Null(BookingIntentJson.Validate(Reply("null", date: "2030-06-02"), Context));
        Assert.Null(BookingIntentJson.Validate("not json", Context));
        Assert.Null(BookingIntentJson.Validate("""{ "date": "2030-06-04" }""", Context));
    }
}
