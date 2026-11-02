using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Domain;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

public class RescheduleCancelTests : IntegrationTestBase
{
    public RescheduleCancelTests(ApiFactory factory) : base(factory)
    {
    }

    // June 2030, so nothing is in the past. London is UTC+1 then.
    private static string Utc(int hour, int minute = 0) => $"2030-06-10T{hour:00}:{minute:00}:00Z";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> BookAsync(HttpClient client, Guid resourceId, int startHour, int endHour)
    {
        var response = await client.PostAsJsonAsync("/api/bookings",
            new { resourceId, start = Utc(startHour), end = Utc(endHour) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, JsonElement booking, string start, string end, int? version = null) =>
        client.PutAsJsonAsync($"/api/bookings/{booking.GetProperty("id").GetGuid()}", new
        {
            resourceId = booking.GetProperty("resourceId").GetGuid(),
            start,
            end,
            version = version ?? booking.GetProperty("version").GetInt32(),
        });

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, JsonElement booking) =>
        client.PostAsync($"/api/bookings/{booking.GetProperty("id").GetGuid()}/cancel", null);

    private async Task SetStatusAsync(Guid bookingId, BookingStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Bookings.Where(b => b.Id == bookingId).ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, status));
    }

    [Fact]
    public async Task A_booking_can_move_within_its_own_slot()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await BookAsync(alice, DemoData.RoomAId, 9, 10);

        var response = await MoveAsync(alice, booking, Utc(9, 30), Utc(10, 30));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await Json(response);
        Assert.Equal(DateTime.Parse(Utc(9, 30)).ToUniversalTime(), moved.GetProperty("start").GetDateTime().ToUniversalTime());
        Assert.NotEqual(booking.GetProperty("version").GetInt32(), moved.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Moving_onto_another_booking_gives_409_slot_taken()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        var mine = await BookAsync(alice, DemoData.RoomAId, 9, 10);
        await BookAsync(bob, DemoData.RoomAId, 11, 12);

        var response = await MoveAsync(alice, mine, Utc(11, 30), Utc(12, 30));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SLOT_TAKEN", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_stale_version_gives_409_stale_version()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await BookAsync(alice, DemoData.RoomAId, 9, 10);

        var first = await MoveAsync(alice, booking, Utc(9, 30), Utc(10, 30));
        var second = await MoveAsync(alice, booking, Utc(13), Utc(14));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await Json(second);
        Assert.Equal("STALE_VERSION", problem.GetProperty("code").GetString());
        Assert.Contains("changed since you opened it", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Cancelling_twice_gives_200_both_times_and_frees_the_slot()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        var booking = await BookAsync(alice, DemoData.RoomAId, 9, 10);

        var first = await CancelAsync(alice, booking);
        var second = await CancelAsync(alice, booking);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Cancelled", (await Json(second)).GetProperty("status").GetString());
        await BookAsync(bob, DemoData.RoomAId, 9, 10);
    }

    [Fact]
    public async Task Cancelling_a_finished_booking_gives_409_invalid_state()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await BookAsync(alice, DemoData.RoomAId, 9, 10);
        await SetStatusAsync(booking.GetProperty("id").GetGuid(), BookingStatus.Completed);

        var response = await CancelAsync(alice, booking);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_cancelled_booking_cannot_be_rescheduled()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await BookAsync(alice, DemoData.RoomAId, 9, 10);
        var cancelled = await Json(await CancelAsync(alice, booking));

        var response = await MoveAsync(alice, cancelled, Utc(13), Utc(14));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task My_bookings_split_into_upcoming_soonest_first_and_past_newest_first()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var first = await BookAsync(alice, DemoData.RoomAId, 9, 10);
        var second = await BookAsync(alice, DemoData.RoomBId, 11, 12);
        var later = await BookAsync(alice, DemoData.RoomAId, 13, 14);
        // Now it is 12:30 UTC on 10 June: the first two have ended, the third is still to come.
        Factory.Clock.UtcNow = new DateTime(2030, 6, 10, 12, 30, 0, DateTimeKind.Utc);

        var upcoming = await Json(await alice.GetAsync("/api/bookings?mine=true&when=upcoming"));
        var past = await Json(await alice.GetAsync("/api/bookings?mine=true&when=past"));
        var bad = await alice.GetAsync("/api/bookings?mine=true&when=soon");

        Assert.Equal([later.GetProperty("id").GetGuid()], upcoming.GetProperty("items").EnumerateArray().Select(b => b.GetProperty("id").GetGuid()));
        Assert.Equal([second.GetProperty("id").GetGuid(), first.GetProperty("id").GetGuid()],
            past.GetProperty("items").EnumerateArray().Select(b => b.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task A_member_cannot_change_someone_elses_booking_but_an_admin_can()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var booking = await BookAsync(alice, DemoData.RoomAId, 9, 10);

        Assert.Equal(HttpStatusCode.Forbidden, (await MoveAsync(bob, booking, Utc(13), Utc(14))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelAsync(bob, booking)).StatusCode);

        var moved = await MoveAsync(admin, booking, Utc(13), Utc(14));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(DemoData.AliceId, (await Json(moved)).GetProperty("ownerId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(admin, booking)).StatusCode);
    }
}
