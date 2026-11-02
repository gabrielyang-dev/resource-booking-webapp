using Booking.Api.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.UnitTests;

public class PostgresErrorsTests
{
    private static PostgresException Postgres(string sqlState) => new("message", "ERROR", "ERROR", sqlState);

    [Fact]
    public void Finds_the_code_inside_efs_wrappers()
    {
        // This is how a deadlock arrives: EF marks it transient and wraps the DbUpdateException.
        var wrapped = new InvalidOperationException("transient failure",
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.DeadlockDetected)));

        Assert.Equal(PostgresErrorCodes.DeadlockDetected, PostgresErrors.SqlState(wrapped));
        Assert.True(PostgresErrors.IsSlotContention(wrapped));
    }

    [Fact]
    public void An_exclusion_violation_is_slot_contention_but_other_errors_are_not()
    {
        Assert.True(PostgresErrors.IsSlotContention(new DbUpdateException("x", Postgres(PostgresErrorCodes.ExclusionViolation))));
        Assert.False(PostgresErrors.IsSlotContention(new DbUpdateException("x", Postgres(PostgresErrorCodes.UniqueViolation))));
        Assert.Null(PostgresErrors.SqlState(new InvalidOperationException("no database involved")));
    }
}
