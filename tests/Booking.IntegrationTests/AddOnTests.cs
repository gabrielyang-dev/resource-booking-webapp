using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;

namespace Booking.IntegrationTests;

// The seed has Sandwiches (24 hours' notice, 08:00 to 15:00) and Coffee service (2 hours, 07:00 to 17:00) in London.
// The fake clock is Monday 3 June 2030, 08:00 UTC (09:00 London).
public class AddOnTests : IntegrationTestBase
{
    public AddOnTests(ApiFactory factory) : base(factory)
    {
    }

    private static string London(int day, int hour) =>
        new DateTimeOffset(2030, 6, day, hour, 0, 0, TimeSpan.FromHours(1)).ToString("o");

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static object Body(int day, int startHour, object[]? addOns = null) => new
    {
        resourceId = DemoData.RoomAId,
        start = London(day, startHour),
        end = London(day, startHour + 1),
        addOns,
    };

    private static object Sandwiches(int quantity) => new { addOnId = DemoData.SandwichesId, quantity };

    [Fact]
    public async Task A_booking_with_sandwiches_lists_the_requested_order()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var response = await alice.PostAsJsonAsync("/api/bookings", Body(10, 12, [Sandwiches(6)]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = Assert.Single((await Json(response)).GetProperty("addOns").EnumerateArray());
        Assert.Equal("Sandwiches", order.GetProperty("name").GetString());
        Assert.Equal(6, order.GetProperty("quantity").GetInt32());
        Assert.Equal("Requested", order.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Moving_a_booking_inside_the_sandwiches_lead_time_is_refused()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await Json(await alice.PostAsJsonAsync("/api/bookings", Body(10, 12, [Sandwiches(6)])));

        // Today (3 June) at 12:00 is only 3 hours away. The add-ons are left out, so the current order is kept and checked again.
        var response = await alice.PutAsJsonAsync($"/api/bookings/{booking.GetProperty("id").GetGuid()}", new
        {
            resourceId = DemoData.RoomAId,
            start = London(3, 12),
            end = London(3, 13),
            version = booking.GetProperty("version").GetInt32(),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var codes = (await Json(response)).GetProperty("violations").EnumerateArray().Select(v => v.GetProperty("code").GetString());
        Assert.Contains("ADD_ON_TOO_LATE", codes);
    }

    [Fact]
    public async Task Cancelling_a_booking_cancels_its_orders()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var booking = await Json(await alice.PostAsJsonAsync("/api/bookings", Body(10, 12, [Sandwiches(6)])));

        var cancelled = await Json(await alice.PostAsync($"/api/bookings/{booking.GetProperty("id").GetGuid()}/cancel", null));

        Assert.Equal("Cancelled", cancelled.GetProperty("addOns")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sandwiches_for_a_booking_starting_at_three_are_refused()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var response = await alice.PostAsJsonAsync("/api/bookings", Body(10, 15, [Sandwiches(6)]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ADD_ON_UNAVAILABLE", (await Json(response)).GetProperty("violations").EnumerateArray().Select(v => v.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task An_add_on_from_another_location_is_400_and_only_admins_manage_add_ons()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var paris = await Json(await admin.PostAsJsonAsync("/api/locations", new { name = "Paris office", timeZone = "Europe/Paris" }));
        var croissants = await admin.PostAsJsonAsync("/api/add-ons", new
        {
            locationId = paris.GetProperty("id").GetGuid(), name = "Croissants", leadTimeMinutes = 60, availableFrom = "07:00", availableUntil = "11:00",
        });
        Assert.Equal(HttpStatusCode.Created, croissants.StatusCode);
        var croissantsId = (await Json(croissants)).GetProperty("id").GetGuid();

        var wrongPlace = await alice.PostAsJsonAsync("/api/bookings", Body(10, 9, [new { addOnId = croissantsId, quantity = 4 }]));
        var memberCreate = await alice.PostAsJsonAsync("/api/add-ons", new { locationId = DemoData.LondonId, name = "Cake" });

        Assert.Equal(HttpStatusCode.BadRequest, wrongPlace.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberCreate.StatusCode);
    }
}
