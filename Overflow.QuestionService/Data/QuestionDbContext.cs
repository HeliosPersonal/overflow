using Microsoft.EntityFrameworkCore;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Data;

public class QuestionDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Question> Questions { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Answer> Answers { get; set; }
}