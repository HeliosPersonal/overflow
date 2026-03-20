using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Features.Questions.Queries;

public record GetQuestionByIdQuery(string QuestionId) : IQuery<Question?>;

public class GetQuestionByIdHandler(
    QuestionDbContext db) : IRequestHandler<GetQuestionByIdQuery, Question?>
{
    public async Task<Question?> Handle(GetQuestionByIdQuery request, CancellationToken ct)
    {
        var question = await db.Questions
            .AsNoTracking()
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == request.QuestionId, ct);

        if (question is not null)
        {
            await db.Questions.Where(x => x.Id == request.QuestionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ViewCount, x => x.ViewCount + 1), ct);
        }

        return question;
    }
}