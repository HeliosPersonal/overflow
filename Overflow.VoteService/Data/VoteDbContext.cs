using Microsoft.EntityFrameworkCore;
using Overflow.VoteService.Models;

namespace Overflow.VoteService.Data;

public class VoteDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Vote> Votes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vote>(x =>
        {
            x.HasIndex(v => new { v.UserId, v.TargetType, v.TargetId }).IsUnique();
        });
    }
}