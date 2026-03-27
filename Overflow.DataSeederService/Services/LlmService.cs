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
                // Add per-attempt timeout (2 minutes) to prevent hanging on slow/stuck generations
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(TimeSpan.FromMinutes(2));
                
                var started = DateTime.UtcNow;
                await chat.SendAsync(prompt.User, tools: null, imagesAsBase64: null, format: "json", attemptCts.Token)
                    .StreamToEndAsync(t => sb.Append(t));

                logger.LogDebug("[Answer] LLM responded in {Elapsed:F1}s",
                    (DateTime.UtcNow - started).TotalSeconds);
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                logger.LogWarning("[Answer] Attempt {A}/{Max}: LLM request timed out (2min limit). Question may be too complex for the model.", 
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

            var responseText = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(responseText))
            {
                logger.LogWarning("[Answer] Attempt {A}/{Max}: LLM returned empty response", attempt, maxAttempts);
                continue;
            }

            // Clean up common JSON issues from LLM output
            responseText = CleanJsonResponse(responseText);

            try
            {
                var dto = JsonSerializer.Deserialize<AnswerGenerationDto>(responseText, JsonOptions);
                if (dto != null) return dto;

                logger.LogWarning("[Answer] Attempt {A}/{Max}: deserialised to null", attempt, maxAttempts);
            }
            catch (JsonException ex)
            {
                logger.LogWarning("[Answer] Attempt {A}/{Max}: JSON parse error — {Msg}. Response preview: {Preview}",
                    attempt, maxAttempts, ex.Message, responseText.Length > 200 ? responseText[..200] + "..." : responseText);
            }
        }

        return null;
    }

    /// <summary>Cleans common JSON formatting issues from LLM output.</summary>
    private static string CleanJsonResponse(string response)
    {
        // Remove markdown code fences if present
        if (response.StartsWith("```json"))
            response = response["```json".Length..];
        else if (response.StartsWith("```"))
            response = response["```".Length..];
        
        if (response.EndsWith("```"))
            response = response[..^3];

        // Trim whitespace
        response = response.Trim();

        // If response has trailing text after closing brace, remove it
        var lastBrace = response.LastIndexOf('}');
        if (lastBrace > 0 && lastBrace < response.Length - 1)
            response = response[..(lastBrace + 1)];

        return response;
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
            // Add 30 second timeout for ranking (simpler task)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            
            await chat.SendAsync(user, cts.Token).StreamToEndAsync(t => sb.Append(t));
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning("[SelectBest] LLM ranking request timed out (30s limit). Falling back to first variant.");
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
            "You are an experienced software developer providing technical help.\n" +
            "\n" +
            "IMPORTANT:\n" +
            "- Respond with ONLY valid JSON\n" +
            "- No markdown code fences (```)\n" +
            "- No extra text before or after the JSON object\n" +
            "- Use plain text in all fields - no markdown formatting\n" +
            "- Keep code examples short and focused\n" +
            "- Be direct and helpful";

        var tagsHint = tags.Count > 0 ? $"\nTags: {string.Join(", ", tags)}" : "";

        var user =
            $"Question: {title}{tagsHint}\n\n{content}\n\n" +
            "Provide a helpful answer in this exact JSON format:\n" +
            "{\n" +
            "  \"explanation\": \"Brief explanation of the issue\",\n" +
            "  \"fix_steps\": [\"Step 1\", \"Step 2\"],\n" +
            "  \"code_snippet\": \"example code without backticks\",\n" +
            "  \"language\": \"language name\",\n" +
            "  \"notes\": \"optional tip or empty string\"\n" +
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