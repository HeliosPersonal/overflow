using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Overflow.EstimationService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EstimationDbContext>
{
    public EstimationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EstimationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=estimation_design;");
        return new EstimationDbContext(optionsBuilder.Options);
    }
}