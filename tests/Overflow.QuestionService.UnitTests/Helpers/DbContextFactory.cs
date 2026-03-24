using Microsoft.EntityFrameworkCore;

namespace Overflow.QuestionService.UnitTests.Helpers;

/// <summary>
/// Creates fresh InMemory DbContext instances for each test to ensure isolation.
/// </summary>
public static class DbContextFactory
{
    public static Data.QuestionDbContext CreateQuestionDb()
    {
        var options = new DbContextOptionsBuilder<Data.QuestionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Data.QuestionDbContext(options);
    }
}