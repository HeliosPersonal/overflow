using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>Runs LLM → critic → repair → HTML → POST pipeline for answers. Also handles answer acceptance.</summary>
public class AnswerService(
    IQuestionApiClient questionApi,
    LlmService llm,
    IOptions<SeederOptions> options,
    ILogger<AnswerService> logger)
{
    private readonly SeederOptions _options = options.Value;

    // ── Post an answer ────────────────────────────────────────────────────────

    public async Task<Answer?> PostAnswerAsync(
        Question question, SeederUser author,
        ComplexityLevel complexity, CancellationToken ct = default)
    {
        var html = await RunAnswerPipelineAsync(question, complexity, ct);
        if (string.IsNullOrWhiteSpace(html))
        {
            logger.LogWarning("LLM pipeline produced no output for question '{Title}' — skipping", question.Title);
            return null;
        }

        try
        {
            var answer = await questionApi.CreateAnswerAsync(
                question.Id,
                new CreateAnswerDto { Content = html },
                $"Bearer {author.Token}", ct);

            logger.LogInformation("Posted answer (ID: {Id}) on question '{Title}'",
                answer.Id, question.Title);
            return answer;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post answer to Question Service");
            return null;
        }
    }

    // ── Accept an answer ──────────────────────────────────────────────────────

    public async Task<bool> AcceptAnswerAsync(
        string questionId, string answerId, SeederUser asker, CancellationToken ct = default)
    {
        try
        {
            await questionApi.AcceptAnswerAsync(questionId, answerId, $"Bearer {asker.Token}", ct);
            logger.LogInformation("Accepted answer {AnswerId} on question {QuestionId}", answerId, questionId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to accept answer {AnswerId}", answerId);
            return false;
        }
    }

    // ── LLM pipeline ──────────────────────────────────────────────────────────

    private async Task<string?> RunAnswerPipelineAsync(
        Question question, ComplexityLevel complexity, CancellationToken ct)
    {
        var questionDto = new QuestionGenerationDto
        {
            Title = question.Title,
            Context = question.Content
        };

        AnswerGenerationDto? answerDto = null;

        for (var attempt = 1; attempt <= _options.MaxGenerationRetries + 1; attempt++)
        {
            var candidate = await llm.GenerateAnswerAsync(questionDto, complexity, ct);
            if (ContentAssembler.ValidateAnswer(candidate).Count == 0)
            {
                answerDto = candidate;
                break;
            }
        }

        if (answerDto == null)
        {
            return null;
        }

        if (_options.EnableCriticPass)
        {
            var critic = await llm.EvaluateAsync(questionDto, answerDto, ct);

            if (critic is { Valid: false } && critic.Issues.Count > 0)
            {
                logger.LogInformation("[Critic] Flagged {Count} issue(s): {Issues}",
                    critic.Issues.Count, string.Join("; ", critic.Issues));

                if (_options.EnableRepairPass)
                {
                    var repaired = await llm.RepairAsync(questionDto, answerDto, critic, ct);
                    if (repaired?.Answer != null && ContentAssembler.ValidateAnswer(repaired.Answer).Count == 0)
                    {
                        answerDto = repaired.Answer;
                    }
                }
            }
        }

        var html = ContentAssembler.BuildAnswerHtml(answerDto, logger);
        return ContentAssembler.ValidateRenderedAnswer(html, answerDto, out _) ? html : null;
    }
}