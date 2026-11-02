using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Infrastructure;

namespace Booking.IntegrationTests;

public class SessionTests : IntegrationTestBase
{
    public SessionTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Signing_in_as_an_unknown_user_gives_404_not_found()
    {
        await SeedAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/session", new { userId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", await CodeOf(response));
    }

    [Fact]
    public async Task Signing_out_when_signed_out_gives_204()
    {
        await SeedAsync();
        var client = Factory.CreateClient();

        var response = await client.DeleteAsync("/api/session");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        await SeedAsync();
        var client = await SignedInClientAsync(DemoData.AliceId);

        await client.DeleteAsync("/api/session");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Member_gets_403_forbidden_on_create_and_reset()
    {
        await SeedAsync();
        var member = await SignedInClientAsync(DemoData.AliceId);

        var create = await member.PostAsJsonAsync("/api/users", new { name = "X", email = "x@example.com", role = "Member" });
        var reset = await member.PostAsync("/api/demo/reset", null);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal("FORBIDDEN", await CodeOf(create));
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Equal("FORBIDDEN", await CodeOf(reset));
    }

    [Fact]
    public async Task Unknown_api_routes_and_bad_ids_give_404_with_a_code()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var unknown = await admin.GetAsync("/api/nothing-here");
        var badId = await admin.GetAsync("/api/users/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", await CodeOf(unknown));
        Assert.Equal(HttpStatusCode.NotFound, badId.StatusCode);
        Assert.Equal("NOT_FOUND", await CodeOf(badId));
    }

    [Fact]
    public async Task Wrong_method_and_wrong_content_type_give_errors_with_a_code()
    {
        await SeedAsync();
        var admin = await SignedInClientAsync(DemoData.AdminId);

        var wrongMethod = await admin.DeleteAsync("/api/users");
        var wrongType = await admin.PostAsync("/api/users", new StringContent("hello", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.NotNull(await CodeOf(wrongMethod));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        Assert.NotNull(await CodeOf(wrongType));
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
