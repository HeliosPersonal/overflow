using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>
///     Generates AI answers via Ollama. Produces N variants and picks the best one.
/// </summary>
public class LlmService(
    IOllamaApiClient ollama,
    IOptions<AiAnswerOptions> options,
    ILogger<LlmService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly AiAnswerOptions _options = options.Value;

    /// <summary>
    ///     Generates <see cref="AiAnswerOptions.AnswerVariants" /> answer variants for the given question,
    ///     scores them, and returns the best rendered HTML. Returns null if all attempts fail.
    /// </summary>
    public async Task<string?> GenerateBestAnswerAsync(
        string questionTitle, string questionContent, List<string> tags, CancellationToken ct = default)
    {
        var variants = new List<AnswerWithScore>();

        for (var i = 0; i < _options.AnswerVariants; i++)
        {
            logger.LogInformation("[Variant {Index}/{Total}] Generating answer for '{Title}'",
                i + 1, _options.AnswerVariants, questionTitle);

            var answer = await GenerateAnswerAsync(questionTitle, questionContent, tags, ct);
            if (answer == null) continue;

            var issues = ValidateAnswer(answer);
            if (issues.Count > 0)
            {
                logger.LogDebug("[Variant {Index}/{Total}] Validation failed: {Issues}",
                    i + 1, _options.AnswerVariants, string.Join("; ", issues));
                continue;
            }

            var html = BuildAnswerHtml(answer);
            if (html.Length < 150)
            {
                logger.LogDebug("[Variant {Index}/{Total}] Rendered HTML too short ({Length} chars)",
                    i + 1, _options.AnswerVariants, html.Length);
                continue;
            }

            variants.Add(new AnswerWithScore { Answer = answer, RenderedHtml = html });
        }

        if (variants.Count == 0)
        {
            logger.LogWarning("All {Count} answer variants failed for '{Title}'",
                _options.AnswerVariants, questionTitle);
            return null;
        }

        if (variants.Count == 1)
            return variants[0].RenderedHtml;

        // Ask the LLM to rank the variants
        var bestIndex = await SelectBestVariantAsync(questionTitle, variants, ct);
        logger.LogInformation("Selected variant {Index}/{Total} as best answer for '{Title}'",
            bestIndex + 1, variants.Count, questionTitle);

        return variants[bestIndex].RenderedHtml;
    }

    // ── Answer generation ─────────────────────────────────────────────────────

    private async Task<AnswerGenerationDto?> GenerateAnswerAsync(
        string title, string content, List<string> tags, CancellationToken ct)
    {
        var maxAttempts = _options.MaxGenerationRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var prompt = BuildAnswerPrompt(title, content, tags);
            var chat = new Chat(ollama, prompt.System)
            {
                Options = new RequestOptions { Temperature = 0.6f }
            };

            var sb = new StringBuilder();
            try
            {
                var started = DateTime.UtcNow;
                await chat.SendAsync(prompt.User, tools: null, imagesAsBase64: null, format: "json", ct)
                    .StreamToEndAsync(t => sb.Append(t));

                logger.LogDebug("[Answer] LLM responded in {Elapsed:F1}s",
                    (DateTime.UtcNow - started).TotalSeconds);
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                logger.LogError(ex, "[Answer] Attempt {A}/{Max}: LLM request timed out or was canceled. Check if Ollama is running at the configured endpoint.", 
                    attempt, maxAttempts);
                continue;
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "[Answer] Attempt {A}/{Max}: Could not connect to Ollama. Check if the service is accessible at the configured URL.", 
                    attempt, maxAttempts);
                continue;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Answer] Attempt {A}/{Max}: LLM request failed with unexpected error", 
                    attempt, maxAttempts);
                continue;
            }

            try
            {
                var dto = JsonSerializer.Deserialize<AnswerGenerationDto>(sb.ToString(), JsonOptions);
                if (dto != null) return dto;

                logger.LogWarning("[Answer] Attempt {A}/{Max}: deserialised to null", attempt, maxAttempts);
            }
            catch (JsonException ex)
            {
                logger.LogWarning("[Answer] Attempt {A}/{Max}: JSON parse error — {Msg}",
                    attempt, maxAttempts, ex.Message);
            }
        }

        return null;
    }

    /// <summary>Returns the 0-based index of the best variant. Falls back to 0 on failure.</summary>
    private async Task<int> SelectBestVariantAsync(
        string questionTitle, List<AnswerWithScore> variants, CancellationToken ct)
    {
        if (variants.Count <= 1) return 0;

        var answersText = string.Join("\n\n---\n\n",
            variants.Select((v, i) => $"Answer {i}:\n{v.RenderedHtml}"));

        const string system =
            "You evaluate technical answers. Respond with ONLY a single number — the index of the best answer.";
        var user =
            $"Question: {questionTitle}\n\n{answersText}\n\n" +
            $"Which answer (0-{variants.Count - 1}) is the most correct and helpful? Reply with the number only.";

        var chat = new Chat(ollama, system)
        {
            Options = new RequestOptions { Temperature = 0.1f }
        };

        var sb = new StringBuilder();
        try
        {
            await chat.SendAsync(user, ct).StreamToEndAsync(t => sb.Append(t));
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "[SelectBest] LLM ranking request timed out or was canceled. Falling back to first variant. Check if Ollama is running at the configured endpoint.");
            return 0;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[SelectBest] Could not connect to Ollama for variant ranking. Falling back to first variant. Check if the service is accessible.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SelectBest] LLM ranking request failed with unexpected error. Falling back to first variant.");
            return 0;
        }

        return int.TryParse(sb.ToString().Trim(), out var idx) && idx >= 0 && idx < variants.Count
            ? idx
            : 0;
    }

    // ── Prompt ────────────────────────────────────────────────────────────────

    private static (string System, string User) BuildAnswerPrompt(
        string title, string content, List<string> tags)
    {
        const string system =
            "You are an experienced software developer answering a StackOverflow question.\n" +
            "\n" +
            "STRICT RULES:\n" +
            "1. Return ONLY valid JSON. No markdown fences. No text outside the JSON object.\n" +
            "2. Each field contains RAW TEXT ONLY — no markdown formatting inside field values.\n" +
            "3. 'explanation': prose only. No code. 1-3 sentences on the root cause.\n" +
            "4. 'fix_steps': array of 1-5 action sentences. Each step is one sentence.\n" +
            "5. 'code_snippet': plain corrected code only. No backticks, no fences.\n" +
            "6. 'notes': one or two sentences of extra tips, or empty string.\n" +
            "7. NEVER write: 'Hope this helps', 'Let me know', 'Thanks', 'Good luck'.\n" +
            "8. NEVER generate vote counts, usernames, badges, or any StackOverflow UI text.";

        var tagsHint = tags.Count > 0 ? $"Tags: {string.Join(", ", tags)}\n" : "";

        var user =
            $"Answer this question.\nTitle: {title}\n{tagsHint}\n{content}\n\n" +
            "Return ONLY this JSON object:\n" +
            "{\n" +
            "  \"explanation\": \"1-3 sentences on the root cause. No code.\",\n" +
            "  \"fix_steps\": [\"First do this.\", \"Then do that.\"],\n" +
            "  \"code_snippet\": \"corrected plain code, no backticks\",\n" +
            "  \"language\": \"language of the code\",\n" +
            "  \"notes\": \"optional extra tip or empty string\"\n" +
            "}";

        return (system, user);
    }

    // ── Validation & HTML rendering ───────────────────────────────────────────

    private static List<string> ValidateAnswer(AnswerGenerationDto dto)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Explanation)) issues.Add("explanation is empty");
        if (string.IsNullOrWhiteSpace(dto.CodeSnippet)) issues.Add("code_snippet is empty");
        else if (dto.CodeSnippet.Trim().Split('\n').Length > 40)
            issues.Add("code_snippet too long");
        return issues;
    }

    private static string BuildAnswerHtml(AnswerGenerationDto dto)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(dto.Explanation))
            sb.Append("<p>").Append(dto.Explanation.Trim()).Append("</p>");

        var steps = dto.FixSteps.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (steps.Count > 0)
        {
            sb.Append("<h3>Fix</h3><ol>");
            foreach (var step in steps)
                sb.Append("<li>").Append(step.Trim()).Append("</li>");
            sb.Append("</ol>");
        }

        if (!string.IsNullOrWhiteSpace(dto.CodeSnippet))
        {
            var lang = NormaliseLanguage(dto.Language);
            sb.Append($"<pre><code class=\"language-{lang}\">")
                .Append(System.Net.WebUtility.HtmlEncode(dto.CodeSnippet.Trim()))
                .Append("</code></pre>");
        }

        if (!string.IsNullOrWhiteSpace(dto.Notes))
            sb.Append("<h3>Notes</h3><p>").Append(dto.Notes.Trim()).Append("</p>");

        return sb.ToString();
    }

    private static string NormaliseLanguage(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "c#" or "csharp" or "cs" => "csharp",
            "js" or "javascript" => "javascript",
            "ts" or "typescript" => "typescript",
            "py" or "python" => "python",
            "rb" or "ruby" => "ruby",
            "go" or "golang" => "go",
            "rs" or "rust" => "rust",
            "java" => "java",
            "kotlin" or "kt" => "kotlin",
            "swift" => "swift",
            "cpp" or "c++" => "cpp",
            "c" => "c",
            "php" => "php",
            "sh" or "bash" or "shell" => "bash",
            "ps" or "powershell" => "powershell",
            "sql" => "sql",
            "html" => "html",
            "css" => "css",
            "yaml" or "yml" => "yaml",
            "json" => "json",
            "xml" => "xml",
            "dockerfile" => "dockerfile",
            _ => ""
        };
}