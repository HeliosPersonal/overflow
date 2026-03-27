using CSharpFunctionalExtensions;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public class AiAnswerService(
    IQuestionApiClient questionApi,
    LlmService llm,
    AiUserProvider aiUserProvider,
    ILogger<AiAnswerService> logger)
{
    public async Task<Result<Answer>> GenerateAndPostAnswerAsync(
        string questionId, string questionTitle, string questionContent,
        List<string> tags, CancellationToken ct = default)
    {
        var userResult = await aiUserProvider.GetUserAsync(ct);
        if (userResult.IsFailure)
        {
            return Result.Failure<Answer>(userResult.Error);
        }

        logger.LogInformation("Generating AI answer for '{Title}' ({Id})", questionTitle, questionId);

        var htmlResult = await GenerateHtmlSafe(questionTitle, questionContent, tags);
        if (htmlResult.IsFailure)
        {
            return Result.Failure<Answer>(htmlResult.Error);
        }

        return await PostWithTokenRefresh(questionId, questionTitle, htmlResult.Value, userResult.Value);
    }

    private async Task<Result<Answer>> PostWithTokenRefresh(
        string questionId, string title, string html, AiUser user)
    {
        // Try with the cached token first
        var result = await PostAnswerSafe(questionId, html, user.Token);
        if (result.IsSuccess)
        {
            logger.LogInformation("AI answered '{Title}' — {AnswerId}", title, result.Value.Id);
            return result;
        }

        // If the cached token failed, refresh and retry once
        logger.LogWarning("Post failed with cached token for {Id}, refreshing: {Error}", questionId, result.Error);

        var tokenResult = await aiUserProvider.GetFreshTokenAsync();
        if (tokenResult.IsFailure)
        {
            logger.LogError("Token refresh failed for {Id}: {Error}", questionId, tokenResult.Error);
            return Result.Failure<Answer>(tokenResult.Error);
        }

        result = await PostAnswerSafe(questionId, html, tokenResult.Value);
        if (result.IsSuccess)
        {
            logger.LogInformation("AI answered '{Title}' — {AnswerId} (after token refresh)", title, result.Value.Id);
        }

        return result;
    }

    private async Task<Result<string>> GenerateHtmlSafe(
        string title, string content, List<string> tags)
    {
        try
        {
            return await llm.GenerateBestAnswerAsync(title, content, tags);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<string>("LLM generation cancelled");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Ollama unreachable");
            return Result.Failure<string>($"Ollama unreachable: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM generation error");
            return Result.Failure<string>(ex.Message);
        }
    }

    private async Task<Result<Answer>> PostAnswerSafe(string questionId, string html, string token)
    {
        try
        {
            var answer = await questionApi.CreateAnswerAsync(
                questionId, new CreateAnswerDto { Content = html }, $"Bearer {token}");

            return answer;
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<Answer>($"QuestionService HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<Answer>($"Post failed: {ex.Message}");
        }
    }
}