using System.Net;
using System.Net.Http.Json;
using Booking.Api.Features.Health;
using Booking.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.IntegrationTests;

public class HealthTests : IntegrationTestBase
{
    public HealthTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Health_reports_connected_when_postgres_answers()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal(new HealthResponse("healthy", "connected"), body);
    }

    [Fact]
    public void Api_uses_the_test_database()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expected = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString).Database;

        Assert.Equal(expected, db.Database.GetDbConnection().Database);
    }
}
