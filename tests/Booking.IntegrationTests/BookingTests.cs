using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Booking.IntegrationTests;

public class BookingTests : IntegrationTestBase
{
    public BookingTests(ApiFactory factory) : base(factory)
    {
    }

    // June 2030: London is on BST (UTC+1), so 10:00 local is 09:00Z.
    private static string Utc(int hour) => $"2030-06-10T{hour:00}:00:00Z";

    private static object Body(Guid resourceId, int startHour, int endHour, int? attendees = null) =>
        new { resourceId, start = Utc(startHour), end = Utc(endHour), attendees };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> CountBookingsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.CountAsync();
    }

    [Fact]
    public async Task Signed_out_cannot_book_or_list()
    {
        await SeedAsync();
        var client = Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 9, 10))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookings?mine=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/resources/{DemoData.RoomAId}/bookings?from={Utc(0)}&to={Utc(23)}")).StatusCode);
    }

    [Fact]
    public async Task A_booking_is_created_confirmed_and_listed_for_its_owner_only()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);

        var created = await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 9, 10, 3));
        var mine = await Json(await alice.GetAsync("/api/bookings?mine=true"));
        var bobs = await Json(await bob.GetAsync("/api/bookings?mine=true"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, mine.GetProperty("totalCount").GetInt32());
        var item = mine.GetProperty("items")[0];
        Assert.Equal("Confirmed", item.GetProperty("status").GetString());
        Assert.Equal("Room A", item.GetProperty("resourceName").GetString());
        Assert.Equal("Europe/London", item.GetProperty("timeZone").GetString());
        Assert.Equal(0, bobs.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task An_overlapping_booking_gets_409_slot_taken_naming_the_local_times()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);
        await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 9, 10));

        var clash = await bob.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = "2030-06-10T09:30:00Z", end = "2030-06-10T10:30:00Z" });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var problem = await Json(clash);
        Assert.Equal("SLOT_TAKEN", problem.GetProperty("code").GetString());
        Assert.Contains("Room A", problem.GetProperty("detail").GetString());
        Assert.Contains("10:00 to 11:00", problem.GetProperty("detail").GetString());
        Assert.Equal(1, await CountBookingsAsync());
    }

    [Fact]
    public async Task Back_to_back_bookings_are_both_created()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bob = await SignedInClientAsync(DemoData.BobId);

        var first = await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 10, 11));
        var second = await bob.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 11, 12));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Twenty_parallel_requests_for_one_slot_give_one_201_and_nineteen_409()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomBId, 9, 10)))));

        Assert.Equal("", string.Join(",", responses.Select(r => (int)r.StatusCode).Order()).Replace("201,409,409,409,409,409,409,409,409,409,409,409,409,409,409,409,409,409,409,409", ""));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await CountBookingsAsync());
    }

    [Fact]
    public async Task The_same_idempotency_key_replays_the_booking_and_creates_one_row()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var first = await Send(alice, "key-1");
        var second = await Send(alice, "key-1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await Json(first)).GetProperty("id").GetGuid(), (await Json(second)).GetProperty("id").GetGuid());
        Assert.Equal(1, await CountBookingsAsync());
    }

    [Fact]
    public async Task Parallel_requests_with_the_same_idempotency_key_create_one_row_and_never_fail()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Send(alice, "race"))));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.Equal(1, await CountBookingsAsync());
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(Body(DemoData.RoomAId, 9, 10)),
        };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Stored_times_read_back_as_utc()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        // An offset of +01:00 is converted: 10:00+01:00 is 09:00Z.
        await alice.PostAsJsonAsync("/api/bookings", new { resourceId = DemoData.RoomAId, start = "2030-06-10T10:00:00+01:00", end = "2030-06-10T11:00:00+01:00" });

        using var scope = Factory.Services.CreateScope();
        var booking = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.AsNoTracking().SingleAsync();

        Assert.Equal(DateTimeKind.Utc, booking.Start.Kind);
        Assert.Equal(DateTimeKind.Utc, booking.End.Kind);
        Assert.Equal(DateTimeKind.Utc, booking.CreatedAt.Kind);
        Assert.Equal(new DateTime(2030, 6, 10, 9, 0, 0, DateTimeKind.Utc), booking.Start);
    }

    [Fact]
    public async Task The_database_rejects_an_overlapping_insert_that_bypasses_the_manager()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 9, 10));

        var overlapping = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync("Confirmed"));
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, overlapping.SqlState);

        // A status that frees the slot is allowed.
        await InsertAsync("Cancelled");
        Assert.Equal(2, await CountBookingsAsync());
    }

    private static async Task InsertAsync(string status)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO \"Bookings\" (\"Id\", \"ResourceId\", \"OwnerId\", \"Start\", \"End\", \"Status\", \"Version\", \"CreatedAt\") " +
            "VALUES (gen_random_uuid(), @resource, @owner, @start, @end, @status, 1, now())", connection);
        command.Parameters.AddWithValue("resource", DemoData.RoomAId);
        command.Parameters.AddWithValue("owner", DemoData.AliceId);
        command.Parameters.AddWithValue("start", new DateTime(2030, 6, 10, 9, 30, 0, DateTimeKind.Utc));
        command.Parameters.AddWithValue("end", new DateTime(2030, 6, 10, 10, 30, 0, DateTimeKind.Utc));
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Bad_input_gives_400_validation_failed()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        var bodies = new object[]
        {
            Body(DemoData.RoomAId, 9, 10, attendees: 0),
            Body(Guid.NewGuid(), 9, 10),
        };

        foreach (var body in bodies)
        {
            var response = await alice.PostAsJsonAsync("/api/bookings", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("VALIDATION_FAILED", (await Json(response)).GetProperty("code").GetString());
        }
        Assert.Equal(0, await CountBookingsAsync());
    }

    [Fact]
    public async Task An_end_before_or_equal_to_the_start_breaks_a_rule_with_422()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        foreach (var body in new[] { Body(DemoData.RoomAId, 10, 9), Body(DemoData.RoomAId, 10, 10) })
        {
            var response = await alice.PostAsJsonAsync("/api/bookings", body);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var codes = (await Json(response)).GetProperty("violations").EnumerateArray().Select(v => v.GetProperty("code").GetString());
            Assert.Contains("END_BEFORE_START", codes);
        }
        Assert.Equal(0, await CountBookingsAsync());
    }

    [Fact]
    public async Task The_day_view_lists_only_slot_holding_bookings_that_overlap_the_period()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);
        await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 9, 10));
        await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomAId, 14, 15));
        await alice.PostAsJsonAsync("/api/bookings", Body(DemoData.RoomBId, 9, 10));

        // [09:30, 14:00) overlaps the first booking and ends exactly where the second starts.
        var rows = await Json(await alice.GetAsync($"/api/resources/{DemoData.RoomAId}/bookings?from=2030-06-10T09:30:00Z&to=2030-06-10T14:00:00Z"));
        var unknown = await alice.GetAsync($"/api/resources/{Guid.NewGuid()}/bookings?from={Utc(0)}&to={Utc(23)}");

        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("Alice Member", rows[0].GetProperty("ownerName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
