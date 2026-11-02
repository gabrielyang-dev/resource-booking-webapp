using Booking.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Health;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    // Long enough for a sleeping Neon database to wake up.
    private static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(10);

    private readonly AppDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
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

    // Give up after the timeout so a dead database never makes the health check hang.
    // Every failure is logged, so the hosting logs show why the database is unreachable.
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
            _logger.LogWarning("Health check: the database did not answer within {Seconds} seconds.", DatabaseTimeout.TotalSeconds);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Health check: could not reach the database.");
            return false;
        }
    }
}

public record HealthResponse(string Api, string Database);
