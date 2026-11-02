using Booking.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Domain.Booking> Bookings => Set<Domain.Booking>();
    public DbSet<RuleSetting> RuleSettings => Set<RuleSetting>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<AddOnOrder> AddOnOrders => Set<AddOnOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AddOn>(addOn =>
        {
            addOn.Property(a => a.Name).HasMaxLength(200);
            addOn.HasOne(a => a.Location).WithMany().HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Restrict);
            addOn.HasOne<User>().WithMany().HasForeignKey(a => a.FulfillerId).OnDelete(DeleteBehavior.SetNull);
            addOn.HasIndex(a => new { a.LocationId, a.Name }).IsUnique();
            addOn.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AddOns_LeadTime", "\"LeadTimeMinutes\" >= 0");
                t.HasCheckConstraint("CK_AddOns_Hours", "\"AvailableUntil\" > \"AvailableFrom\"");
            });
        });

        modelBuilder.Entity<AddOnOrder>(order =>
        {
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            order.HasOne<Domain.Booking>().WithMany(b => b.AddOnOrders).HasForeignKey(o => o.BookingId).OnDelete(DeleteBehavior.Cascade);
            order.HasOne(o => o.AddOn).WithMany().HasForeignKey(o => o.AddOnId).OnDelete(DeleteBehavior.Restrict);
            order.HasIndex(o => o.BookingId);
            order.ToTable(t => t.HasCheckConstraint("CK_AddOnOrders_Quantity", "\"Quantity\" >= 1"));
        });

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(200);
            user.Property(u => u.Email).HasMaxLength(320);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            user.HasIndex(u => u.Email).IsUnique();
        });

        // The case-insensitive unique indexes on names (lower("Name")) are created with SQL in the
        // migration, because EF cannot model an index on an expression.
        modelBuilder.Entity<Location>(location =>
        {
            location.Property(l => l.Name).HasMaxLength(200);
            location.Property(l => l.TimeZone).HasMaxLength(100);
        });

        modelBuilder.Entity<ResourceType>(type =>
        {
            type.Property(t => t.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Resource>(resource =>
        {
            resource.Property(r => r.Name).HasMaxLength(200);
            resource.HasOne(r => r.Location).WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
            resource.HasOne(r => r.ResourceType).WithMany().HasForeignKey(r => r.ResourceTypeId).OnDelete(DeleteBehavior.Restrict);
            resource.HasIndex(r => r.ResourceTypeId);
            resource.ToTable(t => t.HasCheckConstraint("CK_Resources_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" >= 1"));
        });

        // The exclusion constraint that stops double bookings is created with SQL in the migration,
        // because EF cannot model it.
        modelBuilder.Entity<Domain.Booking>(booking =>
        {
            booking.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
            booking.Property(b => b.IdempotencyKey).HasMaxLength(200);
            booking.Property(b => b.Version).IsConcurrencyToken();
            booking.HasOne(b => b.Resource).WithMany().HasForeignKey(b => b.ResourceId).OnDelete(DeleteBehavior.Restrict);
            booking.HasOne(b => b.Owner).WithMany().HasForeignKey(b => b.OwnerId).OnDelete(DeleteBehavior.Restrict);
            booking.HasIndex(b => new { b.ResourceId, b.Start });
            booking.HasIndex(b => new { b.OwnerId, b.Start });
            booking.HasIndex(b => new { b.OwnerId, b.IdempotencyKey }).IsUnique();
            booking.ToTable(t => t.HasCheckConstraint("CK_Bookings_End_After_Start", "\"End\" > \"Start\""));
        });

        // A rule belongs to a resource type (the default) or to one resource (an override), never both.
        modelBuilder.Entity<RuleSetting>(setting =>
        {
            setting.Property(s => s.Kind).HasConversion<string>().HasMaxLength(30);
            setting.Property(s => s.Settings).HasColumnType("jsonb");
            setting.HasOne<ResourceType>().WithMany().HasForeignKey(s => s.ResourceTypeId).OnDelete(DeleteBehavior.Cascade);
            setting.HasOne<Resource>().WithMany().HasForeignKey(s => s.ResourceId).OnDelete(DeleteBehavior.Cascade);
            setting.HasIndex(s => new { s.ResourceTypeId, s.Kind }).IsUnique().HasFilter("\"ResourceTypeId\" IS NOT NULL");
            setting.HasIndex(s => new { s.ResourceId, s.Kind }).IsUnique().HasFilter("\"ResourceId\" IS NOT NULL");
            setting.ToTable(t => t.HasCheckConstraint("CK_RuleSettings_One_Owner",
                "(\"ResourceTypeId\" IS NULL) <> (\"ResourceId\" IS NULL)"));
        });
    }
}
