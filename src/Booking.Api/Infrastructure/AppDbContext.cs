using Booking.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(200);
            user.Property(u => u.Email).HasMaxLength(320);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            user.HasIndex(u => u.Email).IsUnique();
        });
    }
}
