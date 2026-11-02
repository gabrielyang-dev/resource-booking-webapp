using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;

namespace Booking.IntegrationTests;

// The seeded rules: meeting rooms 08:00 to 18:00 Monday to Friday, at most 2 hours, up to 60 days ahead;
// desks 07:00 to 20:00 with an 8-hour daily quota. The fake clock is Monday 3 June 2030, 08:00 UTC.
public class RuleTests : IntegrationTestBase
{
    public RuleTests(ApiFactory factory) : base(factory)
    {
    }

    // 10 June 2030 is a Monday; London is UTC+1, so London hour h is UTC hour h - 1.
    private static string London(int hour, int minute = 0, int day = 10) =>
        new DateTimeOffset(2030, 6, day, hour, minute, 0, TimeSpan.FromHours(1)).ToString("o");

    private static object Body(Guid resourceId, string start, string end, int? attendees = null, Guid? ownerId = null) =>
        new { resourceId, start, end, attendees, ownerId };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<List<string>> ViolationCodesAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("RULE_VIOLATION", problem.GetProperty("code").GetString());
        return problem.GetProperty("violations").EnumerateArray().Select(v => v.GetProperty("code").GetString()!).ToList();
    }

    [Fact]
    public async Task A_booking_that_breaks_two_checks_gets_both_violations()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        // Room A holds 4; the fake clock is 09:00 London on 3 June, so 08:00 to 09:00 that day is in the past.
        var response = await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, London(8, day: 3), London(9, day: 3), attendees: 6));

        var codes = await ViolationCodesAsync(response);
        Assert.Contains("OVER_CAPACITY", codes);
        Assert.Contains("IN_THE_PAST", codes);
    }

    [Fact]
    public async Task A_reschedule_runs_the_checks_again_and_leaves_the_booking_unchanged()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await Json(await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, London(10), London(11), attendees: 4)));

        var response = await alice.PutAsJsonAsync($"/api/bookings/{booking.GetProperty("id").GetGuid()}",
            new { resourceId = DemoData.RoomAId, start = London(10), end = London(11), attendees = 6, version = booking.GetProperty("version").GetInt32() });

        Assert.Contains("OVER_CAPACITY", await ViolationCodesAsync(response));
        var stored = await Json(await alice.GetAsync($"/api/bookings/{booking.GetProperty("id").GetGuid()}"));
        Assert.Equal(4, stored.GetProperty("attendees").GetInt32());
    }

    [Fact]
    public async Task Breaking_opening_hours_and_max_duration_lists_both_reasons()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var response = await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, London(17), London(19, 30)));

        var codes = await ViolationCodesAsync(response);
        Assert.Contains("OUTSIDE_OPENING_HOURS", codes);
        Assert.Contains("TOO_LONG", codes);
    }

    [Fact]
    public async Task A_resource_override_beats_the_type_default()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var put = await admin.PutAsJsonAsync($"/api/resources/{DemoData.RoomBId}/rules",
            new[] { new { kind = "MaxDuration", settings = new { maxMinutes = 240 } } });
        var rules = await Json(await alice.GetAsync($"/api/resources/{DemoData.RoomBId}/rules"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var sources = rules.EnumerateArray().ToDictionary(r => r.GetProperty("kind").GetString()!, r => r.GetProperty("source").GetString());
        Assert.Equal("overridden", sources["MaxDuration"]);
        Assert.Equal("inherited", sources["OpeningHours"]);

        var roomB = await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomBId, London(9), London(12)));
        var roomA = await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, London(9), London(12)));
        Assert.Equal(HttpStatusCode.Created, roomB.StatusCode);
        Assert.Contains("TOO_LONG", await ViolationCodesAsync(roomA));
    }

    [Fact]
    public async Task An_admin_booking_for_a_member_is_judged_by_the_members_quota()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var alice = await SignedInClientAsync(DemoData.AliceId);
        Assert.Equal(HttpStatusCode.Created, (await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.Desk1Id, London(7), London(14)))).StatusCode);

        var forAlice = await admin.PostAsJsonAsync("/api/bookings", Body(DemoData.Desk2Id, London(14), London(16), ownerId: DemoData.AliceId));
        var forAdmin = await admin.PostAsJsonAsync("/api/bookings", Body(DemoData.Desk2Id, London(14), London(16)));

        Assert.Contains("DAILY_QUOTA_EXCEEDED", await ViolationCodesAsync(forAlice));
        Assert.Equal(HttpStatusCode.Created, forAdmin.StatusCode);
    }

    [Fact]
    public async Task Invalid_rule_settings_are_400_and_members_cannot_change_rules()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var badHours = new[] { new { kind = "OpeningHours", settings = new { opens = "18:00", closes = "08:00", days = new[] { "Mon" } } } };

        var bad = await admin.PutAsJsonAsync($"/api/resource-types/{DemoData.MeetingRoomTypeId}/rules", badHours);
        var member = await alice.PutAsJsonAsync($"/api/resource-types/{DemoData.MeetingRoomTypeId}/rules", Array.Empty<object>());

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await Json(bad)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
    }
}
