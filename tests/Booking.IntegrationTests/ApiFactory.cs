using Booking.Api.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.IntegrationTests;

// Starts the real API pointed at the test database, with a tiny fake web root
// (index.html and one script) standing in for the built React app, a fake clock,
// and the background jobs switched off (tests run them directly).
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "booking-web-" + Guid.NewGuid());

    public FakeClock Clock { get; } = new();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html>react app</html>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), "console.log('app');");
        await TestDatabase.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", TestDatabase.ConnectionString);
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseWebRoot(_webRoot);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IClock>(Clock);
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }
}
