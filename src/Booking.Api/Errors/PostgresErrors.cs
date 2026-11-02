using Npgsql;

namespace Booking.Api.Errors;

// Reads the Postgres error code out of a failed save, however EF wrapped it.
public static class PostgresErrors
{
    // EF wraps errors it thinks are transient (like a deadlock) in an InvalidOperationException
    // around the DbUpdateException, so look all the way down.
    public static string? SqlState(Exception exception) => exception switch
    {
        PostgresException postgres => postgres.SqlState,
        { InnerException: not null } => SqlState(exception.InnerException),
        _ => null,
    };

    // Two ways a save fails because someone else wants the same slot:
    // 23P01, the exclusion constraint saw an overlapping booking; or
    // 40P01, two inserts for the same slot each waited for the other (the constraint is checked
    // after the row is written), and Postgres cancelled this one to break the deadlock.
    public static bool IsSlotContention(Exception exception) =>
        SqlState(exception) is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected;
}
