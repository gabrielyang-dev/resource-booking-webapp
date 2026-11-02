using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Api.Abstractions;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Booking.IntegrationTests;

// The shared ApiFactory uses FakeBookingIntentParser. The fake clock is Monday 3 June 2030.
public class BookingIntentTests : IntegrationTestBase
{
    public BookingIntentTests(ApiFactory factory) : base(factory)
    {
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static object Body(string text) => new { text, locationId = DemoData.LondonId };

    [Fact]
    public async Task A_sentence_becomes_search_fields_and_never_a_booking()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        var enabled = await Json(await alice.GetAsync("/api/booking-intents/enabled"));
        var response = await alice.PostAsJsonAsync("/api/booking-intents", Body("a room for 6 tomorrow at 2 for an hour"));

        Assert.True(enabled.GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fields = await Json(response);
        Assert.Equal("Meeting room", fields.GetProperty("resourceTypeName").GetString());
        Assert.Equal("2030-06-04", fields.GetProperty("date").GetString());
        Assert.Equal("14:00", fields.GetProperty("start").GetString());
        Assert.Equal(6, fields.GetProperty("attendees").GetInt32());

        using var scope = Factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.CountAsync());
    }

    [Fact]
    public async Task Not_understood_or_a_failing_parser_says_fill_in_the_form()
    {
        await SeedAsync();
        var alice = await SignedInClientAsync(DemoData.AliceId);

        foreach (var text in new[] { "nonsense", "boom" })
        {
            var response = await alice.PostAsJsonAsync("/api/booking-intents", Body(text));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var problem = await Json(response);
            Assert.Equal("INTENT_NOT_UNDERSTOOD", problem.GetProperty("code").GetString());
            Assert.Contains("Fill in the form instead", problem.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public async Task Without_a_parser_the_feature_is_off()
    {
        await SeedAsync();
        using var noAi = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IBookingIntentParser>()));
        var client = noAi.CreateClient();
        (await client.PostAsJsonAsync("/api/session", new { userId = DemoData.AliceId })).EnsureSuccessStatusCode();

        var enabled = await Json(await client.GetAsync("/api/booking-intents/enabled"));
        var response = await client.PostAsJsonAsync("/api/booking-intents", Body("a room tomorrow"));

        Assert.False(enabled.GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
