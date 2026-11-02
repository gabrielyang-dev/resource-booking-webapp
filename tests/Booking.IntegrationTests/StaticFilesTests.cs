using System.Net;

namespace Booking.IntegrationTests;

public class StaticFilesTests : IntegrationTestBase
{
    public StaticFilesTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Existing_asset_is_served_as_itself()
    {
        var response = await Factory.CreateClient().GetAsync("/assets/app.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("console.log('app');", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_asset_is_404_not_the_react_page()
    {
        var response = await Factory.CreateClient().GetAsync("/assets/nope.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Client_route_gets_the_react_page()
    {
        var response = await Factory.CreateClient().GetAsync("/some/client/route");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("react app", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_the_react_page()
    {
        var response = await Factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("react app", await response.Content.ReadAsStringAsync());
    }
}
