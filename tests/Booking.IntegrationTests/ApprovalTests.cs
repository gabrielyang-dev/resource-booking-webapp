using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Abstractions;
using Booking.Api.Infrastructure;
using Booking.Api.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

// The seeded pool car needs approval from Ada (the admin). 10 June 2030 is a Monday; London is UTC+1.
public class ApprovalTests : IntegrationTestBase
{
    public ApprovalTests(ApiFactory factory) : base(factory)
    {
    }

    private static string London(int hour) => new DateTimeOffset(2030, 6, 10, hour, 0, 0, TimeSpan.FromHours(1)).ToString("o");

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Task<HttpResponseMessage> BookCarAsync(HttpClient client, int start = 9, int end = 17) =>
        client.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.PoolCarId, start = London(start), end = London(end) });

    [Fact]
    public async Task A_car_booking_waits_for_approval_and_still_holds_the_slot()
    {
        await SeedAsync();
        var bob = await SignedInClientAsync(DemoData.BobId);
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var ada = await SignedInClientAsync(DemoData.AdminId);

        var request = await Json(await BookCarAsync(bob));
        var clash = await BookCarAsync(alice, 12, 13);

        Assert.Equal("Pending", request.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var waiting = await Json(await ada.GetAsync("/api/bookings/approvals"));
        Assert.Equal(request.GetProperty("id").GetGuid(), waiting.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(1, (await Json(await ada.GetAsync("/api/notifications/summary"))).GetProperty("unread").GetInt32());
    }

    [Fact]
    public async Task Approving_twice_is_harmless_and_tells_the_owner()
    {
        await SeedAsync();
        var bob = await SignedInClientAsync(DemoData.BobId);
        var ada = await SignedInClientAsync(DemoData.AdminId);
        var id = (await Json(await BookCarAsync(bob))).GetProperty("id").GetGuid();

        var first = await ada.PostAsync($"/api/bookings/{id}/approve", null);
        var second = await ada.PostAsync($"/api/bookings/{id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Confirmed", (await Json(second)).GetProperty("status").GetString());
        var notes = await Json(await bob.GetAsync("/api/notifications"));
        Assert.Equal(1, notes.GetProperty("totalCount").GetInt32());
        Assert.Contains("was approved", notes.GetProperty("items")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Only_the_approver_can_decide()
    {
        await SeedAsync();
        var bob = await SignedInClientAsync(DemoData.BobId);
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var id = (await Json(await BookCarAsync(bob))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsync($"/api/bookings/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync($"/api/bookings/{id}/reject", new { reason = "no" })).StatusCode);
    }

    [Fact]
    public async Task A_rejection_needs_a_reason_frees_the_slot_and_tells_the_owner_why()
    {
        await SeedAsync();
        var bob = await SignedInClientAsync(DemoData.BobId);
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var ada = await SignedInClientAsync(DemoData.AdminId);
        var id = (await Json(await BookCarAsync(bob))).GetProperty("id").GetGuid();

        var noReason = await ada.PostAsJsonAsync($"/api/bookings/{id}/reject", new { reason = "" });
        var rejected = await Json(await ada.PostAsJsonAsync($"/api/bookings/{id}/reject", new { reason = "The car is being serviced." }));
        var approveAfter = await ada.PostAsync($"/api/bookings/{id}/approve", null);

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Equal("The car is being serviced.", rejected.GetProperty("rejectionReason").GetString());
        Assert.Equal(HttpStatusCode.Conflict, approveAfter.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await BookCarAsync(alice, 12, 13)).StatusCode);
        var notes = await Json(await bob.GetAsync("/api/notifications"));
        Assert.Contains("The car is being serviced.", notes.GetProperty("items")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task An_undecided_request_expires_at_its_start_once()
    {
        await SeedAsync();
        var bob = await SignedInClientAsync(DemoData.BobId);
        var id = (await Json(await BookCarAsync(bob))).GetProperty("id").GetGuid();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
        var start = new DateTimeOffset(2030, 6, 10, 9, 0, 0, TimeSpan.FromHours(1)).UtcDateTime;
        var before = await ExpireApprovals.RunOnceAsync(db, notifier, start.AddMinutes(-1));
        var first = await ExpireApprovals.RunOnceAsync(db, notifier, start);
        var second = await ExpireApprovals.RunOnceAsync(db, notifier, start);

        Assert.Equal(0, before);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var booking = await Json(await bob.GetAsync($"/api/bookings/{id}"));
        Assert.Equal("Rejected", booking.GetProperty("status").GetString());
        Assert.Equal(ExpireApprovals.Reason, booking.GetProperty("rejectionReason").GetString());
    }

    [Fact]
    public async Task The_approver_booking_their_own_resource_is_confirmed_straight_away()
    {
        await SeedAsync();
        var ada = await SignedInClientAsync(DemoData.AdminId);

        var own = await Json(await BookCarAsync(ada));

        Assert.Equal("Confirmed", own.GetProperty("status").GetString());
    }
}
