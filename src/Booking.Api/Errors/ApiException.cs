namespace Booking.Api.Errors;

// A business failure that becomes a ProblemDetails response with a "code".
public class ApiException : Exception
{
    public int Status { get; }
    public string Code { get; }
    public Dictionary<string, string[]>? Errors { get; }

    public ApiException(int status, string code, string message, Dictionary<string, string[]>? errors = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Errors = errors;
    }

    public static ApiException NotFound(string message) =>
        new(StatusCodes.Status404NotFound, "NOT_FOUND", message);

    public static ApiException Conflict(string code, string message) =>
        new(StatusCodes.Status409Conflict, code, message);

    public static ApiException Invalid(Dictionary<string, string[]> errors) =>
        new(StatusCodes.Status400BadRequest, "VALIDATION_FAILED",
            "Some fields are not valid. Fix the fields listed and try again.", errors);
}
