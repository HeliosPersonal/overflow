using Microsoft.EntityFrameworkCore;

namespace Overflow.VoteService.UnitTests.Helpers;

public static class DbContextFactory
{
    public static Data.VoteDbContext CreateVoteDb()
    {
        var options = new DbContextOptionsBuilder<Data.VoteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Data.VoteDbContext(options);
    }
}