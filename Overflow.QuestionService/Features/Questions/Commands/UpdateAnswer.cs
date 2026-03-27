using CommandFlow;
using CSharpFunctionalExtensions;
using Ganss.Xss;
using Overflow.QuestionService.Data;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record UpdateAnswerCommand(string QuestionId, string AnswerId, string Content) : ICommand<Result>;

public class UpdateAnswerHandler(
    QuestionDbContext db,
    IHtmlSanitizer sanitizer,
    ILogger<UpdateAnswerHandler> logger) : IRequestHandler<UpdateAnswerCommand, Result>
{
    public async Task<Result> Handle(UpdateAnswerCommand request, CancellationToken ct)
    {
        var answer = await db.Answers.FindAsync([request.AnswerId], ct);
        if (answer is null)
            return Result.Failure(DomainErrors.AnswerNotFound);

        if (answer.QuestionId != request.QuestionId)
            return Result.Failure(DomainErrors.AnswerNotFound);

        answer.Content = sanitizer.Sanitize(request.Content);
        answer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogDebug("Answer updated: {AnswerId} in question {QuestionId}", request.AnswerId, request.QuestionId);
        return Result.Success();
    }
}