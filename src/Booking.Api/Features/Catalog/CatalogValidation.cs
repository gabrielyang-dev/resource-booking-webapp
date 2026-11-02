namespace Booking.Api.Features.Catalog;

public static class CatalogValidation
{
    // Returns the trimmed name, or adds a field error and returns "".
    public static string CleanName(string? name, Dictionary<string, string[]> errors)
    {
        var clean = name?.Trim() ?? "";
        if (clean.Length == 0)
        {
            errors["name"] = ["Enter a name."];
        }
        else if (clean.Length > 200)
        {
            errors["name"] = ["Use 200 characters or fewer for the name."];
        }
        return clean;
    }
}
