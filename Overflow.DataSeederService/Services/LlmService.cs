using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public class LlmService(
    IOllamaApiClient ollama,
    IOptions<AiAnswerOptions> options,
    ILogger<LlmService> logger)
{
    private const int MaxGenerationTokens = 1024;
    private const int MaxRankingTokens = 8;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly AiAnswerOptions _opts = options.Value;
    private bool _modelChecked;

    public async Task<Result<string>> GenerateBestAnswerAsync(
        string title, string content, List<string> tags, CancellationToken ct = default)
    {
        await EnsureModelAvailableAsync(ct);

        var variants = new List<AnswerWithScore>();

        for (var i = 0; i < _opts.AnswerVariants; i++)
        {
            logger.LogInformation("[Variant {I}/{N}] Generating for '{Title}'",
                i + 1, _opts.AnswerVariants, title);

            var dto = await GenerateSingleAnswerAsync(title, content, tags, ct);
            if (dto == null)
            {
                continue;
            }

            var html = AnswerHtmlRenderer.RenderIfValid(dto);
            if (html == null)
            {
                logger.LogDebug("[Variant {I}/{N}] Rejected by validation/length", i + 1, _opts.AnswerVariants);
                continue;
            }

            variants.Add(new AnswerWithScore(dto, html));
        }

        switch (variants.Count)
        {
            case 0:
                return Result.Failure<string>($"All {_opts.AnswerVariants} variants failed");
            case 1:
                return variants[0].RenderedHtml;
        }

        var bestIdx = await RankVariantsAsync(title, variants, ct);
        logger.LogInformation("Selected variant {I}/{N} for '{Title}'", bestIdx + 1, variants.Count, title);
        return variants[bestIdx].RenderedHtml;
    }

    // ── Model management ──────────────────────────────────────────────────

    private async Task EnsureModelAvailableAsync(CancellationToken ct)
    {
        switch (_modelChecked)
        {
            case true:
                return;
            default:
                try
                {
                    var models = await ollama.ListLocalModelsAsync(ct);
                    if (models.Any(m => m.Name.StartsWith(_opts.LlmModel, StringComparison.OrdinalIgnoreCase)))
                    {
                        _modelChecked = true;
                        return;
                    }

                    logger.LogInformation("Pulling model '{Model}'...", _opts.LlmModel);
                    await foreach (var s in ollama.PullModelAsync(new PullModelRequest { Model = _opts.LlmModel }, ct))
                        if (!string.IsNullOrWhiteSpace(s?.Status))
                        {
                            logger.LogInformation("[Pull] {Status}", s.Status);
                        }

                    _modelChecked = true;
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
                {
                    logger.LogWarning(ex, "Cannot verify model '{Model}' — proceeding anyway", _opts.LlmModel);
                }

                break;
        }
    }

    // ── Single answer generation ──────────────────────────────────────────

    private async Task<AnswerGenerationDto?> GenerateSingleAnswerAsync(
        string title, string content, List<string> tags, CancellationToken ct)
    {
        var maxAttempts = _opts.MaxGenerationRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var raw = await ChatWithTimeout(
                BuildAnswerPrompt(title, content, tags),
                TimeSpan.FromSeconds(_opts.GenerationTimeoutSeconds),
                MaxGenerationTokens, ct);

            if (raw == null)
            {
                logger.LogWarning("[Answer] {A}/{Max}: empty or timed out", attempt, maxAttempts);
                continue;
            }

            var cleaned = CleanJson(raw);
            try
            {
                var dto = JsonSerializer.Deserialize<AnswerGenerationDto>(cleaned, JsonOpts);
                if (dto != null)
                {
                    return dto;
                }

                logger.LogWarning("[Answer] {A}/{Max}: deserialized to null", attempt, maxAttempts);
            }
            catch (JsonException ex)
            {
                var preview = cleaned.Length > 200 ? cleaned[..200] + "..." : cleaned;
                logger.LogWarning("[Answer] {A}/{Max}: JSON error — {Msg}. Preview: {P}",
                    attempt, maxAttempts, ex.Message, preview);
            }
        }

        return null;
    }

    // ── Variant ranking ───────────────────────────────────────────────────

    private async Task<int> RankVariantsAsync(
        string title, List<AnswerWithScore> variants, CancellationToken ct)
    {
        if (variants.Count <= 1)
        {
            return 0;
        }

        var answersBlock = string.Join("\n\n---\n\n",
            variants.Select((v, i) => $"Answer {i}:\n{v.RenderedHtml}"));

        var prompt = (
            System: "You evaluate technical answers. Respond with ONLY a single number — the index of the best answer.",
            User: $"Question: {title}\n\n{answersBlock}\n\nBest answer (0-{variants.Count - 1})? Number only."
        );

        var raw = await ChatWithTimeout(prompt, TimeSpan.FromSeconds(_opts.RankingTimeoutSeconds),
            MaxRankingTokens, ct);
        return int.TryParse(raw?.Trim(), out var idx) && idx >= 0 && idx < variants.Count ? idx : 0;
    }

    // ── Chat helper ───────────────────────────────────────────────────────

    private async Task<string?> ChatWithTimeout(
        (string System, string User) prompt, TimeSpan timeout, int maxTokens, CancellationToken ct)
    {
        var chat = new Chat(ollama, prompt.System)
        {
            Options = new RequestOptions { Temperature = 0.6f, NumPredict = maxTokens }
        };

        var sb = new StringBuilder();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            await chat.SendAsync(prompt.User, tools: null, imagesAsBase64: null, format: "json", cts.Token)
                .StreamToEndAsync(t => sb.Append(t));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("LLM chat timed out after {Timeout}s — salvaging {Len} chars of partial response",
                timeout.TotalSeconds, sb.Length);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Ollama unreachable during chat");
            return null;
        }

        var result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    // ── Prompt ────────────────────────────────────────────────────────────

    private static (string System, string User) BuildAnswerPrompt(
        string title, string content, List<string> tags)
    {
        const string system = """
                              You are an experienced software developer providing technical help.

                              Rules:
                              - Respond with ONLY valid JSON — no markdown, no fences, no extra text
                              - Keep the ENTIRE response under 800 tokens
                              - explanation: 1-2 sentences max, directly answering the question
                              - points: 2-4 key points, highlights, or steps relevant to the question; empty array [] if none needed
                              - code_snippet: under 15 lines, no boilerplate; empty string if not applicable
                              - language: programming language of code_snippet; empty string if no code
                              """;

        var tagsHint = tags.Count > 0 ? $"\nTags: {string.Join(", ", tags)}" : "";

        var user = $$"""
                     Question: {{title}}{{tagsHint}}

                     {{content}}

                     Respond in this exact JSON format:
                     {
                       "explanation": "Direct answer or brief explanation",
                       "points": ["Point 1", "Point 2"],
                       "code_snippet": "",
                       "language": ""
                     }
                     """;

        return (system, user);
    }

    // ── JSON cleanup ──────────────────────────────────────────────────────

    private static string CleanJson(string raw)
    {
        raw = raw.Trim();

        if (raw.StartsWith("```json"))
        {
            raw = raw["```json".Length..];
        }
        else if (raw.StartsWith("```"))
        {
            raw = raw["```".Length..];
        }

        if (raw.EndsWith("```"))
        {
            raw = raw[..^3];
        }

        raw = raw.Trim();

        var first = raw.IndexOf('{');
        var last = raw.LastIndexOf('}');
        if (first >= 0 && last > first)
        {
            raw = raw[first..(last + 1)];
        }

        raw = Regex.Replace(raw,
            @"(?<=:""\s*)([^""]*?)(?<!\\)\n([^""]*?"")",
            m => m.Value.Replace("\n", "\\n"),
            RegexOptions.Singleline);

        return raw;
    }
}