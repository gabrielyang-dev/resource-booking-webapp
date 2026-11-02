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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
