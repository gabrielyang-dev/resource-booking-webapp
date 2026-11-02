using System.Net.Http.Json;
using Booking.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

// Inherit from this: the database is emptied before every test.
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ApiFactory Factory { get; }

    protected IntegrationTestBase(ApiFactory factory)
    {
        Factory = factory;
    }

    public Task InitializeAsync() => TestDatabase.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Puts the three demo users into the empty database.
    protected async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await DemoData.ResetAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // A client with its own cookie jar, signed in as the given user.
    protected async Task<HttpClient> SignedInClientAsync(Guid userId)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new { userId });
        response.EnsureSuccessStatusCode();
        return client;
    }
}
