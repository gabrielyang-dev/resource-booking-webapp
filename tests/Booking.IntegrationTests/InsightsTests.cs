using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Domain;
using Booking.Api.Infrastructure;
using Booking.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

public class InsightsTests : IntegrationTestBase
{
    public InsightsTests(ApiFactory factory) : base(factory)
    {
    }

    // London is UTC+1 in June 2030; 10 June is a Monday.
    private static string London(int day, int hour) =>
        new DateTimeOffset(2030, 6, day, hour, 0, 0, TimeSpan.FromHours(1)).ToString("o");

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> BookAsync(HttpClient client, Guid resourceId, int day, int startHour, int endHour)
    {
        var response = await client.PostAsJsonAsync("/api/bookings", new { resourceId, start = London(day, startHour), end = London(day, endHour) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    [Fact]
    public async Task Room_a_booked_10_of_its_50_open_hours_in_a_week_is_20_percent()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var admin = await SignedInClientAsync(DemoData.AdminId);
        for (var day = 10; day <= 14; day++)
        {
            await BookAsync(alice, DemoData.RoomAId, day, 9, 11);
        }
        // A cancelled booking adds nothing.
        var cancelled = await BookAsync(alice, DemoData.RoomAId, 10, 13, 15);
        await alice.PostAsync($"/api/bookings/{cancelled.GetProperty("id").GetGuid()}/cancel", null);

        var result = await Json(await admin.GetAsync("/api/insights/utilisation?from=2030-06-10&to=2030-06-16"));

        var roomA = result.GetProperty("resources").EnumerateArray().Single(r => r.GetProperty("resourceName").GetString() == "Room A");
        Assert.Equal(10, roomA.GetProperty("bookedHours").GetDouble());
        Assert.Equal(50, roomA.GetProperty("openHours").GetDouble());
        Assert.Equal(0.2, roomA.GetProperty("utilisation").GetDouble(), 3);
    }

    [Fact]
    public async Task History_filters_by_user_and_status_and_pages_without_repeats()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        var admin = await SignedInClientAsync(DemoData.AdminId);
        await BookAsync(alice, DemoData.RoomAId, 10, 9, 10);
        await BookAsync(alice, DemoData.RoomAId, 11, 9, 10);
        var third = await BookAsync(alice, DemoData.RoomAId, 12, 9, 10);
        await BookAsync(bob, DemoData.RoomBId, 10, 9, 10);
        await alice.PostAsync($"/api/bookings/{third.GetProperty("id").GetGuid()}/cancel", null);

        var alices = await Json(await admin.GetAsync($"/api/insights/history?userId={DemoData.AliceId}"));
        var cancelled = await Json(await admin.GetAsync($"/api/insights/history?userId={DemoData.AliceId}&status=Cancelled"));
        var page1 = await Json(await admin.GetAsync($"/api/insights/history?userId={DemoData.AliceId}&pageSize=2&page=1"));
        var page2 = await Json(await admin.GetAsync($"/api/insights/history?userId={DemoData.AliceId}&pageSize=2&page=2"));

        Assert.Equal(3, alices.GetProperty("totalCount").GetInt32());
        // Newest first: the 12 June booking leads.
        Assert.Equal(third.GetProperty("id").GetGuid(), alices.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(1, cancelled.GetProperty("totalCount").GetInt32());
        var ids = page1.GetProperty("items").EnumerateArray().Concat(page2.GetProperty("items").EnumerateArray())
            .Select(b => b.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public async Task Members_cannot_see_insights()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/insights/utilisation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/insights/history")).StatusCode);
    }

    [Fact]
    public async Task The_completion_job_completes_ended_bookings_once_and_leaves_cancelled_ones_alone()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var ended = await BookAsync(alice, DemoData.RoomAId, 10, 9, 10);
        var cancelled = await BookAsync(alice, DemoData.RoomBId, 10, 9, 10);
        await BookAsync(alice, DemoData.RoomAId, 10, 14, 15);
        await alice.PostAsync($"/api/bookings/{cancelled.GetProperty("id").GetGuid()}/cancel", null);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var noon = new DateTime(2030, 6, 10, 11, 0, 0, DateTimeKind.Utc);
        var first = await CompleteEndedBookings.RunOnceAsync(db, noon);
        var second = await CompleteEndedBookings.RunOnceAsync(db, noon);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var statuses = await db.Bookings.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Status);
        Assert.Equal(BookingStatus.Completed, statuses[ended.GetProperty("id").GetGuid()]);
        Assert.Equal(BookingStatus.Cancelled, statuses[cancelled.GetProperty("id").GetGuid()]);
    }
}
