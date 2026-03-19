using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Templates;

namespace Overflow.DataSeederService.Services;

/// <summary>Orchestrates the multi-step LLM generation pipeline.</summary>
public class LlmService(
    IOllamaApiClient ollama,
    IOptions<SeederOptions> options,
    ILogger<LlmService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly SeederOptions _options = options.Value;

    public async Task<TopicSeedDto?> GenerateTopicSeedAsync(
        string tag, ComplexityLevel complexity, CancellationToken ct = default)
    {
        var result = await CallStructuredAsync<TopicSeedDto>(
            LlmPrompts.TopicSeed(tag, complexity), "TopicSeed", ct);

        if (result != null)
            logger.LogInformation("[TopicSeed] topic={Topic}, difficulty={Difficulty}, type={Type}",
                result.Topic, result.Difficulty, result.ProblemType);

        return result;
    }

    public async Task<QuestionGenerationDto?> GenerateQuestionAsync(
        TopicSeedDto seed, CancellationToken ct = default)
    {
        var result = await CallStructuredAsync<QuestionGenerationDto>(
            LlmPrompts.StructuredQuestion(seed), "StructuredQuestion", ct);

        if (result != null)
            logger.LogInformation("[Question] title='{Title}', lang={Lang}",
                result.Title, result.Language);

        return result;
    }

    public async Task<AnswerGenerationDto?> GenerateAnswerAsync(
        QuestionGenerationDto question, ComplexityLevel complexity, CancellationToken ct = default)
    {
        var style = ComplexityLevelExtensions.RandomStyle();
        var result = await CallStructuredAsync<AnswerGenerationDto>(
            LlmPrompts.StructuredAnswer(question, style, complexity), "StructuredAnswer", ct);

        if (result != null)
            logger.LogInformation("[Answer] style={Style}, complexity={Complexity}, lang={Lang}",
                style, complexity, result.Language);

        return result;
    }

    public async Task<CriticResultDto?> EvaluateAsync(
        QuestionGenerationDto question, AnswerGenerationDto answer, CancellationToken ct = default)
    {
        var result = await CallStructuredAsync<CriticResultDto>(
            LlmPrompts.Critic(question, answer), "Critic", ct);

        if (result != null)
            logger.LogInformation("[Critic] valid={Valid}, issues={Count}", result.Valid, result.Issues.Count);

        return result;
    }

    public async Task<RepairResultDto?> RepairAsync(
        QuestionGenerationDto question, AnswerGenerationDto answer,
        CriticResultDto critic, CancellationToken ct = default)
    {
        var result = await CallStructuredAsync<RepairResultDto>(
            LlmPrompts.Repair(question, answer, critic), "Repair", ct);

        if (result != null)
            logger.LogInformation("[Repair] question={HasQ}, answer={HasA}",
                result.Question != null, result.Answer != null);

        return result;
    }

    /// <summary>Returns the 0-based index of the best answer. Falls back to random on failure.</summary>
    public async Task<int> SelectBestAnswerIndexAsync(
        string questionTitle, List<string> answerContents, CancellationToken ct = default)
    {
        if (answerContents.Count <= 1)
            return 0;

        var prompt = LlmPrompts.SelectBestAnswer(questionTitle, answerContents);
        var chat = CreateChat(prompt.SystemPrompt);

        var sb = new StringBuilder();
        try
        {
            await chat.SendAsync(prompt.UserPrompt, ct)
                .StreamToEndAsync(t => sb.Append(t));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SelectBestAnswer] LLM request failed");
            return Random.Shared.Next(answerContents.Count);
        }

        return int.TryParse(sb.ToString().Trim(), out var idx) && idx >= 0 && idx < answerContents.Count
            ? idx
            : Random.Shared.Next(answerContents.Count);
    }

    // ── Pipeline internals ────────────────────────────────────────────────────

    /// <summary>
    ///     Calls the LLM with <c>format:"json"</c> so Ollama guarantees valid JSON output,
    ///     then deserialises it as <typeparamref name="T"/>. Retries on deserialisation failure.
    /// </summary>
    private async Task<T?> CallStructuredAsync<T>(
        LlmPrompt prompt, string stepName, CancellationToken ct) where T : class
    {
        var maxAttempts = _options.MaxGenerationRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var chat = CreateChat(prompt.SystemPrompt);
            var sb = new StringBuilder();

            try
            {
                var started = DateTime.UtcNow;

                // format:"json" instructs Ollama to emit only valid JSON — no fences, no prose.
                await chat.SendAsync(prompt.UserPrompt, tools: null, imagesAsBase64: null, format: "json", ct)
                    .StreamToEndAsync(t => sb.Append(t));

                logger.LogDebug("[{Step}] LLM responded in {Elapsed:F1}s",
                    stepName, (DateTime.UtcNow - started).TotalSeconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{Step}] Attempt {A}/{Max}: LLM request failed",
                    stepName, attempt, maxAttempts);
                continue;
            }

            try
            {
                var dto = JsonSerializer.Deserialize<T>(sb.ToString(), JsonOptions);
                if (dto != null)
                    return dto;

                logger.LogWarning("[{Step}] Attempt {A}/{Max}: deserialised to null", stepName, attempt, maxAttempts);
            }
            catch (JsonException ex)
            {
                logger.LogWarning("[{Step}] Attempt {A}/{Max}: JSON parse error — {Msg}",
                    stepName, attempt, maxAttempts, ex.Message);
            }
        }

        logger.LogError("[{Step}] All {Max} attempts failed", stepName, maxAttempts);
        return null;
    }

    /// <summary>Creates a fresh independent chat with optional temperature from the prompt.</summary>
    private Chat CreateChat(string systemPrompt) =>
        new(ollama, systemPrompt)
        {
            Options = new RequestOptions { Temperature = 0.7f }
        };
}