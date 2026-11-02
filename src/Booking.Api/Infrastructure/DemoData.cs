using Booking.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Infrastructure;

// The demo seed. Later PRs add their own tables here.
public static class DemoData
{
    public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AliceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid BobId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Runs at startup: only seeds when the database has no users yet.
    public static async Task SeedIfEmptyAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }
        AddSeedUsers(db);
        await db.SaveChangesAsync();
    }

    // Empties the app tables and inserts the seed again, in one transaction.
    public static async Task ResetAsync(AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Two resets at once would both insert the seed ids and one would hit the primary key.
        // Taking this lock first makes the second reset wait, then run on the already clean table.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN EXCLUSIVE MODE");
        await db.Users.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        AddSeedUsers(db);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static void AddSeedUsers(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = AdminId, Name = "Ada Admin", Email = "ada.admin@example.com", Role = Role.Admin },
            new User { Id = AliceId, Name = "Alice Member", Email = "alice.member@example.com", Role = Role.Member },
            new User { Id = BobId, Name = "Bob Member", Email = "bob.member@example.com", Role = Role.Member });
    }
}
