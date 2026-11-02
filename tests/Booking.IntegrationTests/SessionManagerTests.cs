using System.Security.Claims;
using Booking.Api.Domain;
using Booking.Api.Features.Session;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.IntegrationTests;

public class SessionManagerTests
{
    [Fact]
    public async Task Session_is_kept_when_the_database_cannot_be_reached()
    {
        // Port 1 refuses connections, so every query fails.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=1;Database=nothing;Username=x;Password=x;Timeout=1")
            .Options;
        await using var db = new AppDbContext(options);
        var manager = new SessionManager(db, NullLogger<SessionManager>.Instance);
        var principal = SessionManager.CreatePrincipal(
            new User { Id = Guid.NewGuid(), Name = "Test", Email = "t@example.com", Role = Role.Admin });

        var valid = await manager.IsSessionValidAsync(principal);

        Assert.True(valid);
    }
}
