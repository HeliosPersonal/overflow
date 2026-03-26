using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>Orchestrates AI answer generation and posting to QuestionService.</summary>
public class AiAnswerService(
    IQuestionApiClient questionApi,
    LlmService llm,
    AiUserProvider aiUserProvider,
    ILogger<AiAnswerService> logger)
{
    /// <summary>
    ///     Generates the best AI answer for a question and posts it. Returns the created answer or null on failure.
    /// </summary>
    public async Task<Answer?> GenerateAndPostAnswerAsync(
        string questionId, string questionTitle, string questionContent,
        List<string> tags, CancellationToken ct = default)
    {
        var aiUser = await aiUserProvider.GetUserAsync(ct);
        if (aiUser == null)
        {
            logger.LogWarning("AI user not available — skipping answer for question {QuestionId}", questionId);
            return null;
        }

        logger.LogInformation("Generating AI answer for question '{Title}' ({QuestionId})",
            questionTitle, questionId);

        string? html;
        try
        {
            html = await llm.GenerateBestAnswerAsync(questionTitle, questionContent, tags, ct);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex,
                "LLM request timed out or was canceled for question '{Title}' ({QuestionId}). Check if Ollama is running and accessible.",
                questionTitle, questionId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM request failed for question '{Title}' ({QuestionId})",
                questionTitle, questionId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            logger.LogWarning("LLM produced no usable answer for question '{Title}' ({QuestionId})",
                questionTitle, questionId);
            return null;
        }

        logger.LogInformation(
            "LLM generated answer for question '{Title}' ({QuestionId}), obtaining Keycloak token to post...",
            questionTitle, questionId);

        var token = await aiUserProvider.GetFreshTokenAsync(ct);
        if (token == null)
        {
            logger.LogError(
                "Could not obtain Keycloak token for AI user — cannot post answer for {QuestionId}. " +
                "Answer content was successfully generated but not posted. " +
                "Check KeycloakAdminService logs above for the specific cause (timeout, bad credentials, or connectivity).",
                questionId);
            return null;
        }

        try
        {
            var answer = await questionApi.CreateAnswerAsync(
                questionId,
                new CreateAnswerDto { Content = html },
                $"Bearer {token}", ct);

            logger.LogInformation("AI answered question '{Title}' — answer ID: {AnswerId}",
                questionTitle, answer.Id);
            return answer;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post AI answer for question {QuestionId}", questionId);
            return null;
        }
    }
}