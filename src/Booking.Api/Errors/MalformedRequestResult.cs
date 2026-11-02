using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Booking.Api.Errors;

// The 400 for a body that cannot be read at all (bad JSON, wrong types).
public class MalformedRequestResult : IActionResult
{
    private readonly ModelStateDictionary _modelState;

    public MalformedRequestResult(ModelStateDictionary modelState)
    {
        _modelState = modelState;
    }

    public Task ExecuteResultAsync(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (key, entry) in _modelState)
        {
            if (entry.Errors.Count > 0)
            {
                var field = string.IsNullOrEmpty(key) ? "body" : key.TrimStart('$', '.');
                errors[field] = ["This value could not be read. Check it and try again."];
            }
        }
        return ProblemWriter.WriteAsync(context.HttpContext, 400, "VALIDATION_FAILED",
            "The request could not be read. Check the fields listed and try again.", errors);
    }
}
