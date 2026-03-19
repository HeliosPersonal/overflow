using Microsoft.EntityFrameworkCore;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Data;

public class ProfileDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles { get; set; }
}