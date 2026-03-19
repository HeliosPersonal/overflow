using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Overflow.VoteService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VoteDbContext>
{
    public VoteDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VoteDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=vote_design;");
        return new VoteDbContext(optionsBuilder.Options);
    }
}