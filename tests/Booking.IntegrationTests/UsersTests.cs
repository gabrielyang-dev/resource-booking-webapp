using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

public class UsersTests : IntegrationTestBase
{
    public UsersTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Signed_out_requests_get_401_problem_details_not_a_redirect()
    {
        await SeedAsync();
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("NOT_SIGNED_IN", await CodeOf(response));
    }

    [Fact]
    public async Task Member_gets_403_on_admin_endpoints()
    {
        await SeedAsync();
        var client = await SignedInClientAsync(DemoData.AliceId);

        var list = await client.GetAsync("/api/users");
        var reset = await client.PostAsync("/api/demo/reset", null);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal("FORBIDDEN", await CodeOf(list));
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_stored_role_for_each_user()
    {
        await SeedAsync();

        var admin = await SignedInClientAsync(DemoData.AdminId);
        var member = await SignedInClientAsync(DemoData.AliceId);

        Assert.Equal("Admin", (await Json(await admin.GetAsync("/api/me"))).GetProperty("role").GetString());
        Assert.Equal("Member", (await Json(await member.GetAsync("/api/me"))).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Duplicate_email_in_any_case_gives_409_email_taken()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var first = await admin.PostAsJsonAsync("/api/users", new { name = "Test User", email = "test@example.com", role = "Member" });
        var second = await admin.PostAsJsonAsync("/api/users", new { name = "Other", email = "TEST@example.com", role = "Member" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("EMAIL_TAKEN", await CodeOf(second));
    }

    [Fact]
    public async Task Reset_restores_the_seed_and_keeps_the_admin_signed_in()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        await admin.PostAsJsonAsync("/api/users", new { name = "Extra", email = "extra@example.com", role = "Member" });
        await admin.PutAsJsonAsync($"/api/users/{DemoData.AliceId}", new { name = "Renamed", email = "alice.member@example.com", role = "Member" });

        var reset = await admin.PostAsync("/api/demo/reset", null);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var page = await Json(await admin.GetAsync("/api/users"));
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        var names = page.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "Ada Admin", "Alice Member", "Bob Member" }, names);
    }

    [Fact]
    public async Task Pagination_lists_every_user_once_in_a_stable_order()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        // Two users with the same name: the id breaks the tie.
        await admin.PostAsJsonAsync("/api/users", new { name = "Twin", email = "twin1@example.com", role = "Member" });
        await admin.PostAsJsonAsync("/api/users", new { name = "Twin", email = "twin2@example.com", role = "Member" });

        var first = await ReadAllPages(admin);
        var second = await ReadAllPages(admin);

        Assert.Equal(5, first.Distinct().Count());
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task Out_of_range_paging_gives_400(string query)
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.GetAsync("/api/users?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await CodeOf(response));
    }

    [Fact]
    public async Task Demoting_the_only_admin_gives_409_last_admin()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PutAsJsonAsync($"/api/users/{DemoData.AdminId}",
            new { name = "Ada Admin", email = "ada.admin@example.com", role = "Member" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("LAST_ADMIN", await CodeOf(response));
        Assert.Equal("Admin", (await Json(await admin.GetAsync("/api/me"))).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Cookie_is_rejected_when_the_users_role_changes()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var created = await Json(await admin.PostAsJsonAsync("/api/users", new { name = "Second", email = "second@example.com", role = "Admin" }));
        var secondId = created.GetProperty("id").GetGuid();
        var second = await SignedInClientAsync(secondId);

        await admin.PutAsJsonAsync($"/api/users/{secondId}", new { name = "Second", email = "second@example.com", role = "Member" });

        var response = await second.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_is_rejected_when_the_user_is_gone_after_a_reset()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        var created = await Json(await admin.PostAsJsonAsync("/api/users", new { name = "Temp", email = "temp@example.com", role = "Member" }));
        var temp = await SignedInClientAsync(created.GetProperty("id").GetGuid());

        await admin.PostAsync("/api/demo/reset", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await temp.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Bad_input_returns_400_listing_every_bad_field_and_unknown_id_returns_404()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var bad = await admin.PostAsJsonAsync("/api/users", new { email = "not-an-email", role = "Member" });
        var unknown = await admin.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { name = "X", email = "x@example.com", role = "Member" });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await Json(bad)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", await CodeOf(unknown));
    }

    [Fact]
    public async Task Reset_restores_seed_roles_and_running_it_twice_is_harmless()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        await admin.PostAsJsonAsync("/api/users", new { name = "Second", email = "second@example.com", role = "Admin" });
        await admin.PutAsJsonAsync($"/api/users/{DemoData.AliceId}", new { name = "Alice Member", email = "alice.member@example.com", role = "Admin" });

        var first = await admin.PostAsync("/api/demo/reset", null);
        var second = await admin.PostAsync("/api/demo/reset", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var items = (await Json(await admin.GetAsync("/api/users"))).GetProperty("items").EnumerateArray().ToList();
        var roles = items.Select(u => u.GetProperty("role").GetString()).ToList();
        Assert.Equal(new[] { "Admin", "Member", "Member" }, roles);
    }

    [Fact]
    public async Task Concurrent_resets_all_succeed_and_leave_exactly_the_seed()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        for (var round = 0; round < 5; round++)
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => admin.PostAsync("/api/demo/reset", null)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            var page = await Json(await admin.GetAsync("/api/users"));
            Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        }
    }

    [Fact]
    public async Task List_is_ordered_by_name_then_id()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);
        for (var i = 0; i < 4; i++)
        {
            await admin.PostAsJsonAsync("/api/users", new { name = "Twin", email = $"twin{i}@example.com", role = "Member" });
        }

        var items = (await Json(await admin.GetAsync("/api/users?pageSize=100"))).GetProperty("items").EnumerateArray().ToList();
        var pairs = items.Select(u => (Name: u.GetProperty("name").GetString()!, Id: u.GetProperty("id").GetGuid())).ToList();

        Assert.Equal(7, pairs.Count);
        var twins = pairs.Where(p => p.Name == "Twin").Select(p => p.Id).ToList();
        Assert.Equal(twins.OrderBy(id => id).ToList(), twins);
        Assert.Equal(pairs.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(), pairs.Select(p => p.Name).ToList());
    }

    [Fact]
    public async Task A_huge_page_number_gives_400_not_500()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.GetAsync("/api/users?page=2147483647&pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await CodeOf(response));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Admin, Member")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task Only_the_exact_role_names_are_accepted(string role)
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var response = await admin.PostAsJsonAsync("/api/users", new { name = "Role Test", email = "role@example.com", role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("role", out _));
    }

    [Fact]
    public async Task Two_admins_demoting_each_other_at_once_never_leave_zero_admins()
    {
        for (var round = 0; round < 15; round++)
        {
            await SeedAsync();
            var admin = await SignedInClientAsync(DemoData.AdminId);
            var created = await Json(await admin.PostAsJsonAsync("/api/users", new { name = "Second", email = "second@example.com", role = "Admin" }));
            var secondId = created.GetProperty("id").GetGuid();
            var second = await SignedInClientAsync(secondId);

            var demoteSecond = admin.PutAsJsonAsync($"/api/users/{secondId}", new { name = "Second", email = "second@example.com", role = "Member" });
            var demoteFirst = second.PutAsJsonAsync($"/api/users/{DemoData.AdminId}", new { name = "Ada Admin", email = "ada.admin@example.com", role = "Member" });
            var responses = await Task.WhenAll(demoteSecond, demoteFirst);

            // The loser gets 409 LAST_ADMIN, or 401 if its cookie went stale once the other demotion
            // committed. Both are correct; what must never happen is two 200s, a 500, or zero admins.
            Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
            Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1, "both demotions succeeded");
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Users.CountAsync(u => u.Role == Booking.Api.Domain.Role.Admin) >= 1, "no admin left");
        }
    }

    private static async Task<List<string>> ReadAllPages(HttpClient client)
    {
        var ids = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var body = await Json(await client.GetAsync($"/api/users?page={page}&pageSize=2"));
            Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
            ids.AddRange(body.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("id").GetString()!));
        }
        return ids;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("code").GetString();
}
