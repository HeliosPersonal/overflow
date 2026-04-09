using Overflow.Contracts;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService.MessageHandlers;

public class QuestionCreatedHandler(
    AiAnswerService aiAnswerService,
    ILogger<QuestionCreatedHandler> logger)
{
    public async Task HandleAsync(QuestionCreated message, CancellationToken ct)
    {
        logger.LogInformation("QuestionCreated — {Id}, '{Title}'", message.QuestionId, message.Title);

        var result = await aiAnswerService.GenerateAndPostAnswerAsync(
            message.QuestionId, message.Title, message.Content, message.Tags, ct);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"AI answer failed for {message.QuestionId}: {result.Error}");
        }

        logger.LogInformation("AI answer posted — {AnswerId} on {QuestionId}",
            result.Value.Id, message.QuestionId);
    }
}