using Overflow.Contracts;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService.MessageHandlers;

/// <summary>
///     Wolverine handler for <see cref="QuestionCreated" /> events.
///     Generates an AI answer and posts it to the QuestionService.
/// </summary>
public class QuestionCreatedHandler(
    AiAnswerService aiAnswerService,
    ILogger<QuestionCreatedHandler> logger)
{
    public async Task HandleAsync(QuestionCreated message, CancellationToken ct)
    {
        logger.LogInformation(
            "Received QuestionCreated event — QuestionId: {QuestionId}, Title: '{Title}', Tags: [{Tags}]",
            message.QuestionId, message.Title, string.Join(", ", message.Tags));

        var answer = await aiAnswerService.GenerateAndPostAnswerAsync(
            message.QuestionId,
            message.Title,
            message.Content,
            message.Tags,
            ct);

        if (answer != null)
        {
            logger.LogInformation(
                "AI answer posted — AnswerId: {AnswerId} on QuestionId: {QuestionId}",
                answer.Id, message.QuestionId);
        }
        else
        {
            logger.LogWarning("AI answer generation/posting failed for QuestionId: {QuestionId}", message.QuestionId);
        }
    }
}