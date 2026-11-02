using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;
using Npgsql;

namespace Booking.IntegrationTests;

public class CatalogTests : IntegrationTestBase
{
    public CatalogTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Signed_out_gets_401_and_a_member_gets_403_on_every_write()
    {
        await SeedAsync();
        var signedOut = Factory.CreateClient();
        var member = await SignedInClientAsync(DemoData.AliceId);
        var location = new { name = "New", timeZone = "Europe/London" };
        var type = new { name = "New" };
        var resource = new { locationId = DemoData.LondonId, resourceTypeId = DemoData.DeskTypeId, name = "New", capacity = 1 };

        Assert.Equal(HttpStatusCode.Unauthorized, (await signedOut.GetAsync("/api/resources")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/locations", location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/api/locations/{DemoData.LondonId}", location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/locations/{DemoData.LondonId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/resource-types", type)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/api/resource-types/{DemoData.DeskTypeId}", type)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/resource-types/{DemoData.DeskTypeId}")).StatusCode);
        var post = await member.PostAsJsonAsync("/api/resources", resource);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal("FORBIDDEN", await CodeOf(post));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/api/resources/{DemoData.RoomAId}", resource)).StatusCode);
    }

    [Fact]
    public async Task Locations_can_be_created_read_updated_and_deleted()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var created = await admin.PostAsJsonAsync("/api/locations", new { name = "New York office", timeZone = "America/New_York" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("id").GetGuid();

        var updated = await admin.PutAsJsonAsync($"/api/locations/{id}", new { name = "NYC office", timeZone = "America/New_York" });
        var read = await Json(await admin.GetAsync($"/api/locations/{id}"));
        var list = await Json(await admin.GetAsync("/api/locations"));
        var deleted = await admin.DeleteAsync($"/api/locations/{id}");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("NYC office", read.GetProperty("name").GetString());
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/locations/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("GMT Standard Time")]
    [InlineData("Eastern Standard Time")]
    [InlineData("gmt standard time")]
    [InlineData("pacific standard time")]
    [InlineData("europe/london")]
    [InlineData("")]
    public async Task A_time_zone_that_is_not_an_iana_id_gives_400_with_a_time_zone_error(string timeZone)
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PostAsJsonAsync("/api/locations", new { name = "Somewhere", timeZone });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await CodeOf(response));
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("timeZone", out _));
    }

    [Fact]
    public async Task America_New_York_is_accepted()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PostAsJsonAsync("/api/locations", new { name = "New York", timeZone = "America/New_York" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Resource_types_can_be_created_updated_and_deleted_when_unused()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var created = await admin.PostAsJsonAsync("/api/resource-types", new { name = "Parking space" });
        var id = (await Json(created)).GetProperty("id").GetGuid();
        var updated = await admin.PutAsJsonAsync($"/api/resource-types/{id}", new { name = "Bay" });
        var read = await Json(await admin.GetAsync($"/api/resource-types/{id}"));
        var deleted = await admin.DeleteAsync($"/api/resource-types/{id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Bay", read.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Resources_can_be_created_read_and_updated_and_carry_location_and_type_names()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var created = await admin.PostAsJsonAsync("/api/resources",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Room C", capacity = 6 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await Json(created);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal("London office", body.GetProperty("locationName").GetString());
        Assert.Equal("Europe/London", body.GetProperty("timeZone").GetString());
        Assert.Equal("Meeting room", body.GetProperty("resourceTypeName").GetString());
        Assert.True(body.GetProperty("active").GetBoolean());

        var updated = await admin.PutAsJsonAsync($"/api/resources/{id}",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Room C", capacity = (int?)null, active = true });
        var read = await Json(await admin.GetAsync($"/api/resources/{id}"));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("capacity").ValueKind);
    }

    [Fact]
    public async Task Filters_by_location_type_and_both_and_unknown_ids_give_an_empty_page()
    {
        await SeedAsync();
        var member = await SignedInClientAsync(DemoData.AliceId);
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var paris = (await Json(await admin.PostAsJsonAsync("/api/locations", new { name = "Paris office", timeZone = "Europe/Paris" }))).GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync("/api/resources", new { locationId = paris, resourceTypeId = DemoData.DeskTypeId, name = "Desk 1", capacity = 1 });

        Assert.Equal(8, await Total(member, "/api/resources?locationId=" + DemoData.LondonId));
        Assert.Equal(1, await Total(member, "/api/resources?locationId=" + paris));
        Assert.Equal(3, await Total(member, "/api/resources?resourceTypeId=" + DemoData.MeetingRoomTypeId));
        Assert.Equal(4, await Total(member, "/api/resources?resourceTypeId=" + DemoData.DeskTypeId));
        Assert.Equal(3, await Total(member, $"/api/resources?locationId={DemoData.LondonId}&resourceTypeId={DemoData.DeskTypeId}"));
        Assert.Equal(0, await Total(member, "/api/resources?locationId=" + Guid.NewGuid()));
        Assert.Equal(0, await Total(member, "/api/resources?resourceTypeId=" + Guid.NewGuid()));
    }

    [Fact]
    public async Task Members_do_not_see_inactive_resources_but_admins_can_ask_for_them()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var member = await SignedInClientAsync(DemoData.AliceId);

        await Deactivate(admin, DemoData.RoomBId);

        Assert.Equal(7, await Total(member, "/api/resources"));
        Assert.Equal(7, await Total(member, "/api/resources?includeInactive=true"));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/resources/{DemoData.RoomBId}")).StatusCode);
        Assert.Equal(7, await Total(admin, "/api/resources"));
        Assert.Equal(8, await Total(admin, "/api/resources?includeInactive=true"));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/resources/{DemoData.RoomBId}")).StatusCode);

        await admin.PutAsJsonAsync($"/api/resources/{DemoData.RoomBId}",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Room B", capacity = 8, active = true });
        Assert.Equal(8, await Total(member, "/api/resources"));
    }

    [Fact]
    public async Task Duplicate_names_ignoring_case_give_409_name_taken()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var location = await admin.PostAsJsonAsync("/api/locations", new { name = "london OFFICE", timeZone = "Europe/London" });
        var type = await admin.PostAsJsonAsync("/api/resource-types", new { name = "meeting ROOM" });
        var resource = await admin.PostAsJsonAsync("/api/resources",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "room a", capacity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, location.StatusCode);
        Assert.Equal("NAME_TAKEN", await CodeOf(location));
        Assert.Equal("NAME_TAKEN", await CodeOf(type));
        Assert.Equal(HttpStatusCode.Conflict, resource.StatusCode);
        Assert.Equal("NAME_TAKEN", await CodeOf(resource));
    }

    [Fact]
    public async Task The_same_resource_name_is_fine_in_another_location()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var paris = (await Json(await admin.PostAsJsonAsync("/api/locations", new { name = "Paris office", timeZone = "Europe/Paris" }))).GetProperty("id").GetGuid();

        var response = await admin.PostAsJsonAsync("/api/resources",
            new { locationId = paris, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Room A", capacity = 4 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_location_or_type_in_use_gives_409_in_use_naming_the_count()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var location = await admin.DeleteAsync($"/api/locations/{DemoData.LondonId}");
        var type = await admin.DeleteAsync($"/api/resource-types/{DemoData.MeetingRoomTypeId}");

        Assert.Equal(HttpStatusCode.Conflict, location.StatusCode);
        Assert.Equal("IN_USE", await CodeOf(location));
        Assert.Contains("8 resources", (await Json(location)).GetProperty("detail").GetString());
        Assert.Equal("IN_USE", await CodeOf(type));
        Assert.Contains("3 resources", (await Json(type)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/locations/{DemoData.LondonId}")).StatusCode);
    }

    [Fact]
    public async Task Bad_resource_input_gives_400_listing_every_bad_field()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PostAsJsonAsync("/api/resources",
            new { locationId = Guid.NewGuid(), resourceTypeId = Guid.NewGuid(), capacity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("locationId", out _));
        Assert.True(errors.TryGetProperty("resourceTypeId", out _));
        Assert.True(errors.TryGetProperty("capacity", out _));
    }

    [Fact]
    public async Task Paging_lists_every_resource_once_in_name_then_id_order()
    {
        await SeedAsync();
        var member = await SignedInClientAsync(DemoData.AliceId);

        var names = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var body = await Json(await member.GetAsync($"/api/resources?page={page}&pageSize=3"));
            Assert.Equal(8, body.GetProperty("totalCount").GetInt32());
            names.AddRange(body.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString()!));
        }

        Assert.Equal(8, names.Distinct().Count());
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal).ToList(), names);
    }

    [Fact]
    public async Task Reset_restores_the_catalogue()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        await admin.PostAsJsonAsync("/api/resources",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Room C", capacity = 6 });
        await Deactivate(admin, DemoData.RoomBId);
        var extraType = (await Json(await admin.PostAsJsonAsync("/api/resource-types", new { name = "Extra" }))).GetProperty("id").GetGuid();
        await admin.DeleteAsync($"/api/resource-types/{extraType}");
        await admin.DeleteAsync($"/api/resource-types/{DemoData.EquipmentTypeId}");
        await admin.PostAsJsonAsync("/api/locations", new { name = "Paris office", timeZone = "Europe/Paris" });

        var reset = await admin.PostAsync("/api/demo/reset", null);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(1, await Total(admin, "/api/locations"));
        Assert.Equal(4, await Total(admin, "/api/resource-types"));
        Assert.Equal(8, await Total(admin, "/api/resources?includeInactive=true"));
        Assert.Equal(8, await Total(admin, "/api/resources"));
    }

    [Fact]
    public async Task Concurrent_resets_leave_exactly_the_seed()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        for (var round = 0; round < 3; round++)
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => admin.PostAsync("/api/demo/reset", null)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        }
        Assert.Equal(8, await Total(admin, "/api/resources"));
        Assert.Equal(1, await Total(admin, "/api/locations"));
    }

    [Fact]
    public async Task Renaming_to_a_case_variant_of_its_own_name_is_allowed()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var location = await admin.PutAsJsonAsync($"/api/locations/{DemoData.LondonId}", new { name = "LONDON OFFICE", timeZone = "Europe/London" });
        var type = await admin.PutAsJsonAsync($"/api/resource-types/{DemoData.DeskTypeId}", new { name = "DESK" });
        var resource = await admin.PutAsJsonAsync($"/api/resources/{DemoData.RoomAId}",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "ROOM A", capacity = 4, active = true });

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        Assert.Equal(HttpStatusCode.OK, type.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resource.StatusCode);
        Assert.Equal("ROOM A", (await Json(resource)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Renaming_onto_another_items_name_gives_409_name_taken()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var paris = (await Json(await admin.PostAsJsonAsync("/api/locations", new { name = "Paris office", timeZone = "Europe/Paris" }))).GetProperty("id").GetGuid();

        var location = await admin.PutAsJsonAsync($"/api/locations/{paris}", new { name = "london office", timeZone = "Europe/Paris" });
        var type = await admin.PutAsJsonAsync($"/api/resource-types/{DemoData.DeskTypeId}", new { name = "car" });
        var resource = await admin.PutAsJsonAsync($"/api/resources/{DemoData.RoomBId}",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "room a", capacity = 8, active = true });

        Assert.Equal(HttpStatusCode.Conflict, location.StatusCode);
        Assert.Equal("NAME_TAKEN", await CodeOf(location));
        Assert.Equal(HttpStatusCode.Conflict, type.StatusCode);
        Assert.Equal("NAME_TAKEN", await CodeOf(type));
        Assert.Equal(HttpStatusCode.Conflict, resource.StatusCode);
        Assert.Equal("NAME_TAKEN", await CodeOf(resource));
    }

    [Fact]
    public async Task Unknown_ids_give_404_on_get_and_put()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var unknown = Guid.NewGuid();
        var resource = new { locationId = DemoData.LondonId, resourceTypeId = DemoData.DeskTypeId, name = "X", capacity = 1, active = true };

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/locations/{unknown}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/resource-types/{unknown}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/resources/{unknown}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/locations/{unknown}", new { name = "X", timeZone = "Europe/London" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/resource-types/{unknown}", new { name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/resources/{unknown}", resource)).StatusCode);
    }

    [Fact]
    public async Task A_member_can_read_locations_and_resource_types()
    {
        await SeedAsync();
        var member = await SignedInClientAsync(DemoData.AliceId);

        Assert.Equal(1, await Total(member, "/api/locations"));
        Assert.Equal(4, await Total(member, "/api/resource-types"));
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/locations/{DemoData.LondonId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/resource-types/{DemoData.DeskTypeId}")).StatusCode);
    }

    [Theory]
    [InlineData(10_000, HttpStatusCode.Created)]
    [InlineData(10_001, HttpStatusCode.BadRequest)]
    public async Task Capacity_has_a_maximum_of_10000(int capacity, HttpStatusCode expected)
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PostAsJsonAsync("/api/resources",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Big hall", capacity });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("capacity", out _));
        }
    }

    [Fact]
    public async Task A_resource_put_without_active_gives_400()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PutAsJsonAsync($"/api/resources/{DemoData.RoomAId}",
            new { locationId = DemoData.LondonId, resourceTypeId = DemoData.MeetingRoomTypeId, name = "Room A", capacity = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("active", out _));
    }

    [Theory]
    [InlineData("europe/london")]
    [InlineData("EUROPE/LONDON")]
    public async Task A_time_zone_in_the_wrong_case_is_rejected(string timeZone)
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PostAsJsonAsync("/api/locations", new { name = "Case test", timeZone });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_in_use_message_for_a_location_says_to_move_resources_and_counts_inactive_ones()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.DeleteAsync($"/api/locations/{DemoData.LondonId}");

        var detail = (await Json(response)).GetProperty("detail").GetString()!;
        Assert.Contains("another location", detail);
        Assert.Contains("active or inactive", detail);
        Assert.DoesNotContain("deactivate", detail);
    }

    // A reset that deadlocks with a catalogue write (Postgres 40P01) must retry, not fail with 500.
    // The test inserts a resource, which holds a lock on Locations (the foreign key check). It waits until
    // the reset is blocked on that lock, then asks for a lock on Users that the reset already holds:
    // a real deadlock that Postgres resolves after about a second.
    [Fact]
    public async Task Reset_survives_a_deadlock_with_a_catalogue_write()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var insert = new NpgsqlCommand(
            "INSERT INTO \"Resources\" (\"Id\", \"LocationId\", \"ResourceTypeId\", \"Name\", \"IsActive\") " +
            $"VALUES (gen_random_uuid(), '{DemoData.LondonId}', '{DemoData.DeskTypeId}', 'Deadlock test', true)", connection, transaction))
        {
            await insert.ExecuteNonQueryAsync();
        }

        var reset = admin.PostAsync("/api/demo/reset", null);
        await WaitUntilResetIsBlockedAsync();
        try
        {
            await using var touchUsers = new NpgsqlCommand(
                "UPDATE \"Users\" SET \"Name\" = \"Name\" WHERE false", connection, transaction);
            await touchUsers.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch (PostgresException)
        {
            // Postgres picked this side as the deadlock victim; the reset then simply proceeds.
        }

        Assert.Equal(HttpStatusCode.NoContent, (await reset).StatusCode);
        Assert.Equal(8, await Total(admin, "/api/resources"));
    }

    private static async Task WaitUntilResetIsBlockedAsync()
    {
        await using var watcher = new NpgsqlConnection(TestDatabase.ConnectionString);
        await watcher.OpenAsync();
        for (var i = 0; i < 100; i++)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE 'LOCK TABLE%'", watcher);
            if ((long)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }
            await Task.Delay(50);
        }
        throw new InvalidOperationException("The reset never blocked on the lock.");
    }

    private static async Task Deactivate(HttpClient admin, Guid resourceId)
    {
        var current = await Json(await admin.GetAsync($"/api/resources/{resourceId}"));
        var response = await admin.PutAsJsonAsync($"/api/resources/{resourceId}", new
        {
            locationId = current.GetProperty("locationId").GetGuid(),
            resourceTypeId = current.GetProperty("resourceTypeId").GetGuid(),
            name = current.GetProperty("name").GetString(),
            capacity = current.GetProperty("capacity").GetInt32(),
            active = false,
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> Total(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("totalCount").GetInt32();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("code").GetString();
}
