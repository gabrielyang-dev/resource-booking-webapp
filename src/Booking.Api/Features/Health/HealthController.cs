using Booking.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Health;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(3);

    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseIsUp = await CanReachDatabase(cancellationToken);
        if (databaseIsUp)
        {
            return Ok(new HealthResponse("healthy", "connected"));
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("healthy", "unreachable"));
    }

    // Give up after a few seconds so a dead database never makes the health check hang.
    private async Task<bool> CanReachDatabase(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DatabaseTimeout);
        try
        {
            return await _db.Database.CanConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

public record HealthResponse(string Api, string Database);
