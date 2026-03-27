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
    ///     Throws exceptions for infrastructure failures (will be retried by Wolverine).
    /// </summary>
    public async Task<Answer?> GenerateAndPostAnswerAsync(
        string questionId, string questionTitle, string questionContent,
        List<string> tags, CancellationToken ct = default)
    {
        var aiUser = await aiUserProvider.GetUserAsync(ct);
        if (aiUser == null)
        {
            // Infrastructure failure - AI user should exist but doesn't
            logger.LogError("AI user not available — cannot answer question {QuestionId}", questionId);
            throw new InvalidOperationException("AI user is not available - check bootstrap logs");
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
            // This could be infrastructure (Ollama down) or just a slow question
            // Throw to retry - if it keeps failing, will go to DLQ
            throw;
        }
        catch (HttpRequestException ex)
        {
            // Infrastructure failure - Ollama unreachable
            logger.LogError(ex, "Cannot connect to Ollama for question '{Title}' ({QuestionId})", questionTitle, questionId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM request failed for question '{Title}' ({QuestionId})",
                questionTitle, questionId);
            // Unknown exception - let Wolverine retry
            throw;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            // Business failure - LLM couldn't generate a good answer
            // This is expected for some questions - don't retry
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
            // Infrastructure failure - should have a token but don't
            logger.LogError(
                "Could not obtain Keycloak token for AI user — cannot post answer for {QuestionId}. " +
                "Answer content was successfully generated but not posted. " +
                "Check KeycloakAdminService logs above for the specific cause (timeout, bad credentials, or connectivity).",
                questionId);
            throw new InvalidOperationException("Failed to obtain Keycloak token for AI user");
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
        catch (HttpRequestException ex)
        {
            // Infrastructure failure - QuestionService unreachable
            logger.LogError(ex, "Cannot connect to QuestionService to post answer for question {QuestionId}", questionId);
            throw;
        }
        catch (Exception ex)
        {
            // Could be 4xx (bad request) or infrastructure failure
            logger.LogError(ex, "Failed to post AI answer for question {QuestionId}", questionId);
            throw;
        }
    }
}