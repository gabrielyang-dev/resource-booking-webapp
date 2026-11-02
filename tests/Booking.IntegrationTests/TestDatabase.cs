using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.IntegrationTests;

// A dedicated database for tests, so the dev database is never touched.
public static class TestDatabase
{
    public const string ConnectionStringVariable = "BOOKING_TEST_CONNECTION";

    private const string LocalDefault =
        "Host=localhost;Port=5432;Database=booking_test;Username=postgres;Password=postgres;Timeout=3";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? LocalDefault;

    // Refuses to touch any database whose name does not end in _test (protects dev and Neon data).
    private static void EnsureTestDatabase()
    {
        var name = new NpgsqlConnectionStringBuilder(ConnectionString).Database;
        if (name is null || !name.EndsWith("_test"))
        {
            throw new InvalidOperationException(
                $"Refusing to run tests against database \"{name}\": the name must end in _test. Check {ConnectionStringVariable}.");
        }
    }

    // Creates the database if needed and applies migrations. Called once per test run.
    public static async Task MigrateAsync()
    {
        EnsureTestDatabase();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    // Empties every app table between tests. Does nothing until the first table exists.
    public static async Task ResetAsync()
    {
        EnsureTestDatabase();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var list = new NpgsqlCommand(
            "SELECT quote_ident(tablename) FROM pg_tables " +
            "WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'", connection))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count == 0)
        {
            return;
        }

        var sql = "TRUNCATE " + string.Join(", ", tables.Select(t => "public." + t)) + " RESTART IDENTITY CASCADE";
        await using var truncate = new NpgsqlCommand(sql, connection);
        await truncate.ExecuteNonQueryAsync();
    }
}
