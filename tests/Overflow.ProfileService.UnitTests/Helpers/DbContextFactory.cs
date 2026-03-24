using Microsoft.EntityFrameworkCore;

namespace Overflow.ProfileService.UnitTests.Helpers;

public static class DbContextFactory
{
    public static Data.ProfileDbContext CreateProfileDb()
    {
        var options = new DbContextOptionsBuilder<Data.ProfileDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Data.ProfileDbContext(options);
    }
}