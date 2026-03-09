using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>Runs the LLM pipeline → HTML assembly → POST to Question Service. Provides query helpers for other jobs.</summary>
public class QuestionService(
    IQuestionApiClient questionApi,
    LlmService llm,
    IOptions<SeederOptions> options,
    ILogger<QuestionService> logger)
{
    private readonly SeederOptions _options = options.Value;

    // ── Create a question ─────────────────────────────────────────────────────

    public async Task<Question?> PostQuestionAsync(
        SeederUser author, ComplexityLevel complexity, CancellationToken ct = default)
    {
        var tags = await GetTagsAsync(ct);
        if (tags.Count == 0)
        {
            logger.LogWarning("No tags found in Question Service — cannot create question");
            return null;
        }

        var tag = tags[Random.Shared.Next(tags.Count)];
        var (title, html) = await RunGenerationPipelineAsync(tag.Slug, complexity, ct);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(html))
        {
            logger.LogWarning("LLM pipeline produced no output for tag '{Tag}' — skipping", tag.Slug);
            return null;
        }

        try
        {
            var question = await questionApi.CreateQuestionAsync(
                new CreateQuestionDto { Title = title, Content = html, Tags = [tag.Slug] },
                $"Bearer {author.Token}", ct);

            logger.LogInformation("Posted question '{Title}' (ID: {Id})", question.Title, question.Id);
            return question;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post question to Question Service");
            return null;
        }
    }

    // ── Query helpers ─────────────────────────────────────────────────────────

    private async Task<List<Tag>> GetTagsAsync(CancellationToken ct = default)
    {
        try
        {
            return await questionApi.GetTagsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch tags");
            return [];
        }
    }

    public async Task<Question?> GetNewestQuestionAsync(CancellationToken ct = default)
    {
        try
        {
            var page = await questionApi.GetQuestionsAsync("newest", 1, 1, ct);
            return page.Items.Count > 0 ? page.Items[0] : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch newest question");
            return null;
        }
    }

    /// <summary>
    ///     Returns up to <paramref name="count" /> recent unaccepted questions that already have answers.
    /// </summary>
    public async Task<List<Question>> GetUnacceptedQuestionsWithAnswersAsync(
        int count = 3, CancellationToken ct = default)
    {
        try
        {
            var page = await questionApi.GetQuestionsAsync("newest", 1, 20, ct);
            var result = new List<Question>();

            foreach (var q in page.Items.Where(q => !q.HasAcceptedAnswer))
            {
                if (result.Count >= count)
                {
                    break;
                }

                var full = await GetQuestionByIdAsync(q.Id, ct);
                if (full?.Answers.Count > 0)
                {
                    result.Add(full);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch unaccepted questions");
            return [];
        }
    }

    private async Task<Question?> GetQuestionByIdAsync(string questionId, CancellationToken ct = default)
    {
        try
        {
            return await questionApi.GetQuestionByIdAsync(questionId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch question {Id}", questionId);
            return null;
        }
    }

    // ── LLM generation pipeline ───────────────────────────────────────────────

    private async Task<(string? title, string? html)> RunGenerationPipelineAsync(
        string tag, ComplexityLevel complexity, CancellationToken ct)
    {
        var seed = await llm.GenerateTopicSeedAsync(tag, complexity, ct);
        if (seed == null)
        {
            return (null, null);
        }

        for (var attempt = 1; attempt <= _options.MaxGenerationRetries + 1; attempt++)
        {
            var dto = await llm.GenerateQuestionAsync(seed, ct);
            var serDto = JsonSerializer.Serialize(dto);
            var issues = ContentAssembler.ValidateQuestion(dto);
            if (issues.Count > 0)
            {
                logger.LogDebug("[QuestionPipeline] Attempt {A}: {Issues}", attempt, string.Join("; ", issues));
                continue;
            }

            var cleanTitle = Regex
                .Replace(dto!.Title, "<[^>]+>", "").Trim();
            var html = ContentAssembler.BuildQuestionHtml(dto, logger);

            if (!ContentAssembler.ValidateRenderedQuestion(html, dto, out var err))
            {
                logger.LogDebug("[QuestionPipeline] Attempt {A}: render validation — {Error}", attempt, err);
                continue;
            }

            return (cleanTitle, html);
        }

        return (null, null);
    }
}