using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Data;

public class EstimationDbContext(DbContextOptions<EstimationDbContext> options) : DbContext(options)
{
    public DbSet<EstimationRoom> Rooms { get; set; }
    public DbSet<EstimationParticipant> Participants { get; set; }
    public DbSet<EstimationVote> Votes { get; set; }
    public DbSet<EstimationRoundHistory> RoundHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EstimationRoom>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

            e.HasMany(r => r.Participants).WithOne(p => p.Room).HasForeignKey(p => p.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Votes).WithOne(v => v.Room).HasForeignKey(v => v.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.RoundHistory).WithOne(h => h.Room).HasForeignKey(h => h.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EstimationVote>(e =>
        {
            e.HasIndex(v => new { v.RoomId, v.RoundNumber, v.ParticipantId }).IsUnique();
        });

        modelBuilder.Entity<EstimationRoundHistory>(e =>
        {
            e.HasIndex(h => new { h.RoomId, h.RoundNumber }).IsUnique();
        });

        modelBuilder.Entity<EstimationParticipant>(e =>
        {
            e.HasIndex(p => new { p.RoomId, p.ParticipantId }).IsUnique();
        });
    }
}