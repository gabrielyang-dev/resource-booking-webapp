using Booking.Api.Domain;
using Booking.Api.Rules;
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

    public static readonly Guid SandwichesId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    public static readonly Guid CoffeeId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

    // Runs at startup. Seeds the users when there are none, and the catalogue when there is none
    // (so a database created before the catalogue existed gets it too).
    public static async Task SeedIfEmptyAsync(AppDbContext db, DateTime nowUtc)
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

        // Rules came after the catalogue: a database that has the seed types but no rules yet gets the defaults.
        if (!await db.RuleSettings.AnyAsync() && await db.ResourceTypes.AnyAsync(t => t.Id == MeetingRoomTypeId))
        {
            AddSeedRules(db);
            await db.SaveChangesAsync();
        }

        // Add-ons came later too: the seed location with no add-ons gets the catering ones.
        if (!await db.AddOns.AnyAsync() && await db.Locations.AnyAsync(l => l.Id == LondonId))
        {
            AddSeedAddOns(db);
            await db.SaveChangesAsync();
        }

        // A database with the seed catalogue but no bookings yet (a fresh one, or one from before bookings) gets the demo bookings.
        if (!await db.Bookings.AnyAsync() && await db.Resources.AnyAsync(r => r.Id == RoomAId))
        {
            db.Bookings.AddRange(DemoBookings.Create(nowUtc));
            await db.SaveChangesAsync();
        }
    }

    // Empties the app tables and inserts the seed again, in one transaction.
    // With nowUtc (the app always passes it) the demo bookings around that time are added too;
    // most tests leave it out so they start with no bookings.
    public static async Task ResetAsync(AppDbContext db, DateTime? nowUtc = null)
    {
        try
        {
            await ResetOnceAsync(db, nowUtc);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // A catalogue write that holds a table lock while it waits for one the reset holds is a deadlock,
            // and Postgres cancels one of the two. If that was the reset, nothing was changed (the transaction
            // rolled back), so running it once more is safe and the admin never sees a 500.
            db.ChangeTracker.Clear();
            await ResetOnceAsync(db, nowUtc);
        }
    }

    private static bool IsDeadlock(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.DeadlockDetected;

    private static async Task ResetOnceAsync(AppDbContext db, DateTime? nowUtc)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Two resets at once would both insert the seed ids and one would hit the primary key.
        // Taking these locks first makes the second reset wait, then run on the already clean tables.
        // One statement, always in this order, so two resets cannot lock in opposite orders and deadlock.
        await db.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"Users\", \"Locations\", \"ResourceTypes\", \"Resources\", \"Bookings\", \"RuleSettings\", " +
            "\"AddOns\", \"AddOnOrders\" IN EXCLUSIVE MODE");
        // Children before parents, because the foreign keys restrict deletes.
        await db.AddOnOrders.ExecuteDeleteAsync();
        await db.Bookings.ExecuteDeleteAsync();
        await db.AddOns.ExecuteDeleteAsync();
        await db.RuleSettings.ExecuteDeleteAsync();
        await db.Resources.ExecuteDeleteAsync();
        await db.Locations.ExecuteDeleteAsync();
        await db.ResourceTypes.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        AddSeedUsers(db);
        AddSeedCatalogue(db);
        AddSeedRules(db);
        AddSeedAddOns(db);
        if (nowUtc is not null)
        {
            db.Bookings.AddRange(DemoBookings.Create(nowUtc.Value));
        }
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

    // Office defaults. Meeting rooms allow at most 2 hours, so "Room A 17:00 to 19:30" shows two violations in the demo.
    private static void AddSeedRules(AppDbContext db)
    {
        string[] weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri"];
        db.RuleSettings.AddRange(
            TypeRule(MeetingRoomTypeId, RuleKind.OpeningHours, new OpeningHoursSettings("08:00", "18:00", weekdays)),
            TypeRule(MeetingRoomTypeId, RuleKind.MaxDuration, new MaxDurationSettings(120)),
            TypeRule(MeetingRoomTypeId, RuleKind.BookingWindow, new BookingWindowSettings(0, 60)),
            TypeRule(DeskTypeId, RuleKind.OpeningHours, new OpeningHoursSettings("07:00", "20:00", weekdays)),
            TypeRule(DeskTypeId, RuleKind.DailyQuota, new DailyQuotaSettings(480)),
            TypeRule(CarTypeId, RuleKind.MaxDuration, new MaxDurationSettings(600)),
            TypeRule(CarTypeId, RuleKind.BookingWindow, new BookingWindowSettings(60, 30)));
    }

    private static RuleSetting TypeRule<T>(Guid typeId, RuleKind kind, T settings) =>
        new() { Id = Guid.NewGuid(), ResourceTypeId = typeId, Kind = kind, Settings = RuleSettings.ToJson(settings) };

    // Catering for the London office. Ada delivers it (fulfillers are users, not a role).
    private static void AddSeedAddOns(AppDbContext db)
    {
        db.AddOns.AddRange(
            new AddOn
            {
                Id = SandwichesId, LocationId = LondonId, Name = "Sandwiches", LeadTimeMinutes = 24 * 60,
                AvailableFrom = new TimeOnly(8, 0), AvailableUntil = new TimeOnly(15, 0), FulfillerId = AdminId,
            },
            new AddOn
            {
                Id = CoffeeId, LocationId = LondonId, Name = "Coffee service", LeadTimeMinutes = 120,
                AvailableFrom = new TimeOnly(7, 0), AvailableUntil = new TimeOnly(17, 0), FulfillerId = AdminId,
            });
    }
}
