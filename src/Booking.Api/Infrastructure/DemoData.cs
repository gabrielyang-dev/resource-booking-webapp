using Booking.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Infrastructure;

// The demo seed. Later PRs add their own tables here.
public static class DemoData
{
    public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AliceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid BobId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid LondonId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public static readonly Guid MeetingRoomTypeId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    public static readonly Guid DeskTypeId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    public static readonly Guid CarTypeId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
    public static readonly Guid EquipmentTypeId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000004");

    public static readonly Guid RoomAId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    public static readonly Guid RoomBId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    public static readonly Guid BoardroomId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    public static readonly Guid Desk1Id = Guid.Parse("cccccccc-0000-0000-0000-000000000004");
    public static readonly Guid Desk2Id = Guid.Parse("cccccccc-0000-0000-0000-000000000005");
    public static readonly Guid Desk3Id = Guid.Parse("cccccccc-0000-0000-0000-000000000006");
    public static readonly Guid PoolCarId = Guid.Parse("cccccccc-0000-0000-0000-000000000007");
    public static readonly Guid ProjectorId = Guid.Parse("cccccccc-0000-0000-0000-000000000008");

    // Runs at startup. Seeds the users when there are none, and the catalogue when there is none
    // (so a database created before the catalogue existed gets it too).
    public static async Task SeedIfEmptyAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            AddSeedUsers(db);
        }
        if (!await db.Locations.AnyAsync() && !await db.ResourceTypes.AnyAsync())
        {
            AddSeedCatalogue(db);
        }
        await db.SaveChangesAsync();
    }

    // Empties the app tables and inserts the seed again, in one transaction.
    public static async Task ResetAsync(AppDbContext db)
    {
        try
        {
            await ResetOnceAsync(db);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // A catalogue write that holds a table lock while it waits for one the reset holds is a deadlock,
            // and Postgres cancels one of the two. If that was the reset, nothing was changed (the transaction
            // rolled back), so running it once more is safe and the admin never sees a 500.
            db.ChangeTracker.Clear();
            await ResetOnceAsync(db);
        }
    }

    private static bool IsDeadlock(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.DeadlockDetected;

    private static async Task ResetOnceAsync(AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Two resets at once would both insert the seed ids and one would hit the primary key.
        // Taking these locks first makes the second reset wait, then run on the already clean tables.
        // One statement, always in this order, so two resets cannot lock in opposite orders and deadlock.
        await db.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"Users\", \"Locations\", \"ResourceTypes\", \"Resources\", \"Bookings\" IN EXCLUSIVE MODE");
        // Children before parents, because the foreign keys restrict deletes.
        await db.Bookings.ExecuteDeleteAsync();
        await db.Resources.ExecuteDeleteAsync();
        await db.Locations.ExecuteDeleteAsync();
        await db.ResourceTypes.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        AddSeedUsers(db);
        AddSeedCatalogue(db);
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

    private static void AddSeedCatalogue(AppDbContext db)
    {
        db.Locations.Add(new Location { Id = LondonId, Name = "London office", TimeZone = "Europe/London" });
        db.ResourceTypes.AddRange(
            new ResourceType { Id = MeetingRoomTypeId, Name = "Meeting room" },
            new ResourceType { Id = DeskTypeId, Name = "Desk" },
            new ResourceType { Id = CarTypeId, Name = "Car" },
            new ResourceType { Id = EquipmentTypeId, Name = "Equipment" });
        db.Resources.AddRange(
            Seed(RoomAId, MeetingRoomTypeId, "Room A", 4),
            Seed(RoomBId, MeetingRoomTypeId, "Room B", 8),
            Seed(BoardroomId, MeetingRoomTypeId, "Boardroom", 12),
            Seed(Desk1Id, DeskTypeId, "Desk 1", 1),
            Seed(Desk2Id, DeskTypeId, "Desk 2", 1),
            Seed(Desk3Id, DeskTypeId, "Desk 3", 1),
            Seed(PoolCarId, CarTypeId, "Pool car", 5),
            Seed(ProjectorId, EquipmentTypeId, "Projector", null));
    }

    private static Resource Seed(Guid id, Guid typeId, string name, int? capacity) =>
        new() { Id = id, LocationId = LondonId, ResourceTypeId = typeId, Name = name, Capacity = capacity, IsActive = true };
}
