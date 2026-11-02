using Booking.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Features.Shared;

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// Shared by every list endpoint: page (default 1) and pageSize (default 20, max 100).
public static class Paging
{
    public const int MaxPageSize = 100;
    public const int MaxPage = 1_000_000;

    public static void Validate(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
        {
            errors["page"] = ["Page must be 1 or more."];
        }
        if (page > MaxPage)
        {
            errors["page"] = [$"Page must be {MaxPage} or less."];
        }
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }
        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
    }

    // The query must already have a stable order.
    public static async Task<PagedResponse<TOut>> ToPageAsync<TIn, TOut>(
        IOrderedQueryable<TIn> ordered, int page, int pageSize, Func<TIn, TOut> map)
    {
        Validate(page, pageSize);
        var total = await ordered.CountAsync();
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResponse<TOut>(rows.Select(map).ToList(), page, pageSize, total);
    }
}
