using Microsoft.EntityFrameworkCore;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Data;

public class ProfileDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserProfile>()
            .Property(p => p.ThemePreference)
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasDefaultValue(ThemePreference.System);
    }
}