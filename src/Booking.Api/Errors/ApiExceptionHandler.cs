using Microsoft.AspNetCore.Diagnostics;

namespace Booking.Api.Errors;

public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ApiException api)
        {
            await ProblemWriter.WriteAsync(context, api.Status, api.Code, api.Message, api.Errors);
            return true;
        }

        _logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);
        await ProblemWriter.WriteAsync(context, 500, "INTERNAL_ERROR",
            "Something went wrong on our side. Try again, and tell an admin if it keeps happening.");
        return true;
    }
}
