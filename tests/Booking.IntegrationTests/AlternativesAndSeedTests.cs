using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Domain;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

public class AlternativesAndSeedTests : IntegrationTestBase
{
    public AlternativesAndSeedTests(ApiFactory factory) : base(factory)
    {
    }

    // 10 June 2030 is a Monday; London is UTC+1.
    private static string London(int hour) =>
        new DateTimeOffset(2030, 6, 10, hour, 0, 0, TimeSpan.FromHours(1)).ToString("o");

    private static DateTime LondonUtc(int hour) => new DateTimeOffset(2030, 6, 10, hour, 0, 0, TimeSpan.FromHours(1)).UtcDateTime;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task A_taken_slot_suggests_the_nearest_free_time_and_a_free_sibling_and_each_can_be_booked()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        await bob.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = London(10), end = London(11) });
        await bob.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = London(9), end = London(10) });

        var response = await alice.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = London(10), end = London(11) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var alternatives = (await Json(response)).GetProperty("alternatives").EnumerateArray().ToList();
        var sameRoom = alternatives.Where(a => a.GetProperty("resourceId").GetGuid() == DemoData.RoomAId).ToList();
        var otherRooms = alternatives.Where(a => a.GetProperty("resourceId").GetGuid() != DemoData.RoomAId).ToList();
        // 09:00 to 11:00 is taken, so 11:00 is the nearest free hour.
        Assert.Equal(LondonUtc(11), sameRoom[0].GetProperty("start").GetDateTime().ToUniversalTime());
        Assert.Contains(otherRooms, a => a.GetProperty("resourceName").GetString() == "Room B");
        Assert.True(sameRoom.Count <= 3 && otherRooms.Count <= 3);

        foreach (var alternative in new[] { sameRoom[0], otherRooms[0] })
        {
            var booked = await alice.PostAsJsonAsync("/api/bookings", new
            {
                resourceId = alternative.GetProperty("resourceId").GetGuid(),
                start = alternative.GetProperty("start").GetDateTime(),
                end = alternative.GetProperty("end").GetDateTime(),
            });
            Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        }
    }

    [Fact]
    public async Task A_booking_that_is_too_long_gets_no_same_resource_time_that_is_also_too_long()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        // Meeting rooms allow 2 hours, so 3 hours on Room A is too long at any time of day.
        var response = await alice.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = London(9), end = London(12) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var alternatives = (await Json(response)).GetProperty("alternatives").EnumerateArray();
        Assert.DoesNotContain(alternatives, a => a.GetProperty("resourceId").GetGuid() == DemoData.RoomAId);
    }

    [Fact]
    public async Task Availability_leaves_out_booked_resources()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        await alice.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = London(10), end = London(11) });

        var start = Uri.EscapeDataString(London(10));
        var end = Uri.EscapeDataString(London(11));
        var result = await Json(await alice.GetAsync($"/api/availability?resourceTypeId={DemoData.MeetingRoomTypeId}&start={start}&end={end}"));

        var names = result.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToList();
        Assert.DoesNotContain("Room A", names);
        Assert.Contains("Room B", names);
    }

    [Fact]
    public async Task The_demo_reset_seeds_past_weeks_a_cancellation_and_a_clash_ready_to_show()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        // Twice in a row, to show the reset is repeatable.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/demo/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/demo/reset", null)).StatusCode);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bookings = await db.Bookings.AsNoTracking().ToListAsync();
            var now = Factory.Clock.UtcNow;
            Assert.Contains(bookings, b => b.Status == BookingStatus.Completed && b.Start < now.AddDays(-21));
            Assert.Contains(bookings, b => b.Status == BookingStatus.Cancelled);
            Assert.All(bookings.Where(b => b.Status == BookingStatus.Completed), b => Assert.True(b.End <= now));
        }

        // The fake clock is Monday 3 June 2030, so the demo day is Tuesday 4 June: Bob holds Room A 10:00 to 11:00.
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var clash = await alice.PostAsJsonAsync("/api/bookings",
            new { resourceId = DemoData.RoomAId, start = "2030-06-04T10:30:00+01:00", end = "2030-06-04T11:30:00+01:00" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);

        // Last full week: Room A is busy but not full.
        var week = await Json(await admin.GetAsync("/api/insights/utilisation?from=2030-05-27&to=2030-06-02"));
        var roomA = week.GetProperty("resources").EnumerateArray().Single(r => r.GetProperty("resourceName").GetString() == "Room A");
        Assert.InRange(roomA.GetProperty("utilisation").GetDouble(), 0.4, 0.8);
    }
}
