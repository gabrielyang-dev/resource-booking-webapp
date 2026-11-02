using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;
using Booking.Api.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

// Seeded meeting rooms require check-in: it opens 15 minutes before the start, and no-shows are released
// 15 minutes after it. 10 June 2030 is a Monday; London is UTC+1.
public class CheckInTests : IntegrationTestBase
{
    public CheckInTests(ApiFactory factory) : base(factory)
    {
    }

    private static string London(int hour, int minute = 0) =>
        new DateTimeOffset(2030, 6, 10, hour, minute, 0, TimeSpan.FromHours(1)).ToString("o");

    private static DateTime LondonUtc(int hour, int minute = 0) =>
        new DateTimeOffset(2030, 6, 10, hour, minute, 0, TimeSpan.FromHours(1)).UtcDateTime;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<Guid> BookRoomAAsync(HttpClient client, int startHour)
    {
        var response = await client.PostAsJsonAsync("/api/bookings",
            new { resourceId = DemoData.RoomAId, start = London(startHour), end = London(startHour + 1) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Checking_in_twice_is_harmless()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var id = await BookRoomAAsync(alice, 10);
        Factory.Clock.UtcNow = LondonUtc(9, 50);

        var first = await alice.PostAsync($"/api/bookings/{id}/check-in", null);
        var second = await alice.PostAsync($"/api/bookings/{id}/check-in", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var booking = await Json(second);
        Assert.Equal("CheckedIn", booking.GetProperty("status").GetString());
        Assert.Equal(LondonUtc(9, 50), booking.GetProperty("checkedInAt").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Checking_in_outside_the_window_is_409()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var id = await BookRoomAAsync(alice, 10);

        Factory.Clock.UtcNow = LondonUtc(9, 44); // a minute before it opens
        var early = await alice.PostAsync($"/api/bookings/{id}/check-in", null);
        Factory.Clock.UtcNow = LondonUtc(10, 15); // the moment it is released
        var late = await alice.PostAsync($"/api/bookings/{id}/check-in", null);

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        var problem = await Json(early);
        Assert.Equal("CHECK_IN_CLOSED", problem.GetProperty("code").GetString());
        Assert.Contains("09:45 to 10:15", problem.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [Fact]
    public async Task A_no_show_is_released_once_and_the_slot_can_be_booked_again()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        var noShow = await BookRoomAAsync(alice, 10);
        var attended = await BookRoomAAsync(bob, 11);
        Factory.Clock.UtcNow = LondonUtc(10, 55);
        await bob.PostAsync($"/api/bookings/{attended}/check-in", null);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await ReleaseNoShows.RunOnceAsync(db, LondonUtc(11, 20));
        var second = await ReleaseNoShows.RunOnceAsync(db, LondonUtc(11, 20));

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal("Released", (await Json(await alice.GetAsync($"/api/bookings/{noShow}"))).GetProperty("status").GetString());
        Assert.Equal("CheckedIn", (await Json(await bob.GetAsync($"/api/bookings/{attended}"))).GetProperty("status").GetString());

        // The released 10:00 slot is free again. Wind the clock back so 10:00 is still in the future, and book it.
        Factory.Clock.UtcNow = LondonUtc(9, 0);
        var rebook = await bob.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = London(10), end = London(11) });
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);
    }
}
