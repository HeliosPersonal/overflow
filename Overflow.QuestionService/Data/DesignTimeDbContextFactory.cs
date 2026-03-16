using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Overflow.QuestionService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuestionDbContext>
{
    public QuestionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QuestionDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=question_design;");
        return new QuestionDbContext(optionsBuilder.Options);
    }
}