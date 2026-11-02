using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Errors;

// Writes every error in one shape: ProblemDetails with a "code" (and "errors" for field problems).
public static class ProblemWriter
{
    public static Task WriteAsync(
        HttpContext context, int status, string code, string detail, Dictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }

    // For responses that have a status but no body yet: unknown route, wrong method, wrong content type.
    public static Task WriteForStatusAsync(HttpContext context)
    {
        var status = context.Response.StatusCode;
        return WriteAsync(context, status, CodeFor(status), "The request could not be completed. Check the address, method and body, then try again.");
    }

    // Adds the default code to ProblemDetails that do not have one yet.
    public static void AddCode(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (!problem.Extensions.ContainsKey("code"))
        {
            problem.Extensions["code"] = CodeFor(problem.Status ?? 500);
        }
    }

    // The default code for a status. Used for errors MVC creates itself (it has no code of its own).
    public static string CodeFor(int status) => status switch
    {
        400 => "VALIDATION_FAILED",
        401 => "NOT_SIGNED_IN",
        403 => "FORBIDDEN",
        404 => "NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED",
        409 => "CONFLICT",
        415 => "UNSUPPORTED_MEDIA_TYPE",
        _ => "ERROR",
    };

    private static string TitleFor(int status) => status switch
    {
        400 => "Bad request",
        401 => "Not signed in",
        403 => "Not allowed",
        404 => "Not found",
        405 => "Method not allowed",
        415 => "Unsupported media type",
        409 => "Conflict",
        _ => "Something went wrong",
    };
}
