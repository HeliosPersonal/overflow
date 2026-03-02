using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Templates;

namespace Overflow.DataSeederService.Services;

public class LlmClient
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly ILogger<LlmClient> _logger;

    public LlmClient(HttpClient httpClient, IOptions<SeederOptions> options, ILogger<LlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "LlmClient initialized - URL: {Url}, Model: {Model}, Enabled: {Enabled}, HttpClient Timeout: {Timeout}s",
            _options.LlmApiUrl, _options.LlmModel, _options.EnableLlmGeneration, _httpClient.Timeout.TotalSeconds);
    }

    public async Task<string?> GenerateAsync(string systemPrompt, string userPrompt,
        CancellationToken cancellationToken = default, int maxTokens = 500, double temperature = 0.7)
    {
        if (!_options.EnableLlmGeneration)
        {
            _logger.LogWarning("LLM generation is disabled");
            return null;
        }

        _logger.LogInformation("Starting LLM request - URL: {Url}, Model: {Model}, Timeout: {Timeout}s",
            _options.LlmApiUrl, _options.LlmModel, _httpClient.Timeout.TotalSeconds);

        var request = new LlmRequest
        {
            model = _options.LlmModel,
            messages = new[]
            {
                new LlmMessage { role = "system", content = systemPrompt },
                new LlmMessage { role = "user", content = userPrompt }
            },
            temperature = temperature,
            max_tokens = maxTokens
        };

        _logger.LogDebug("LLM Request - System: {SystemPrompt}, User: {UserPrompt}",
            systemPrompt.Substring(0, Math.Min(100, systemPrompt.Length)),
            userPrompt.Substring(0, Math.Min(100, userPrompt.Length)));

        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("Sending POST request to LLM API at {Url}...", _options.LlmApiUrl);

            var response = await _httpClient.PostAsJsonAsync(_options.LlmApiUrl, request, cancellationToken);

            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("LLM API responded in {Elapsed}s with status {StatusCode}", elapsed,
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("LLM API returned status {StatusCode}. Response: {Response}",
                    response.StatusCode, errorContent);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<LlmResponse>(cancellationToken);

            if (result?.choices?.Length > 0)
            {
                var content = result.choices[0].message?.content?.Trim();
                _logger.LogInformation("LLM generated {Length} characters of content in {Elapsed}s",
                    content?.Length ?? 0, elapsed);
                return content;
            }

            _logger.LogWarning("LLM returned no content after {Elapsed}s", elapsed);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "LLM request TIMED OUT after {Elapsed}s. HttpClient timeout: {Timeout}s, URL: {Url}",
                elapsed, _httpClient.Timeout.TotalSeconds, _options.LlmApiUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogWarning(ex, "LLM request was CANCELLED after {Elapsed}s", elapsed);
            return null;
        }
        catch (HttpRequestException ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "HTTP REQUEST FAILED after {Elapsed}s. URL: {Url}, Message: {Message}",
                elapsed, _options.LlmApiUrl, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex,
                "UNEXPECTED ERROR generating content with LLM after {Elapsed}s. Type: {ExceptionType}, Message: {Message}",
                elapsed, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    public async Task<(string? title, string? content)> GenerateQuestionTitleAndContentAsync(
        string tag, ContentVariability variability, CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.QuestionTitleAndContent(tag, variability);
        var result = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);

        if (string.IsNullOrWhiteSpace(result))
            return (null, null);

        // Parse the ===TITLE=== / ===BODY=== format
        var titleMarker = "===TITLE===";
        var bodyMarker = "===BODY===";

        var titleIdx = result.IndexOf(titleMarker, StringComparison.OrdinalIgnoreCase);
        var bodyIdx = result.IndexOf(bodyMarker, StringComparison.OrdinalIgnoreCase);

        if (titleIdx < 0 || bodyIdx < 0 || bodyIdx <= titleIdx)
        {
            _logger.LogWarning("LLM response did not follow ===TITLE===/===BODY=== format, attempting fallback parse");
            // Fallback: treat first line as title, rest as body
            var lines = result.Split('\n', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length >= 2)
                return (lines[0], lines[1]);
            return (null, null);
        }

        var title = result.Substring(titleIdx + titleMarker.Length, bodyIdx - titleIdx - titleMarker.Length).Trim();
        var body = result.Substring(bodyIdx + bodyMarker.Length).Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return (null, null);

        return (title, SanitizeHtml(body));
    }

    public async Task<string?> GenerateQuestionTitleAsync(string tag, CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.QuestionTitle(tag);
        return await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);
    }

    public async Task<string?> GenerateQuestionContentAsync(string title, string tag,
        CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.QuestionContent(title, tag);
        var result = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);
        return result is null ? null : SanitizeHtml(result);
    }

    public async Task<string?> GenerateAnswerAsync(string questionTitle, string questionContent,
        CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.Answer(questionTitle, questionContent);
        var result = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);
        return result is null ? null : SanitizeHtml(result);
    }

    /// <summary>
    /// Safety net: strips markdown fences and converts leftover markdown to HTML
    /// in case the model ignored the "HTML only" instruction.
    /// </summary>
    private static string SanitizeHtml(string content)
    {
        content = content.Trim();

        // Strip outer ```html ... ``` or ``` ... ``` wrapper (anywhere, not just anchored)
        var fenceMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"^```[a-zA-Z]*\r?\n([\s\S]*?)```\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        if (fenceMatch.Success)
            content = fenceMatch.Groups[1].Value.Trim();

        // After fence stripping, if it starts with an HTML tag treat as HTML
        if (content.TrimStart().StartsWith('<'))
            return content;

        // Convert markdown → HTML line by line
        var lines = content.Split('\n');
        var sb = new System.Text.StringBuilder();
        bool inCodeBlock = false;
        string listTag = "";   // "ul" or "ol" — tracks which list is open

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            // ── Fenced code block ─────────────────────────────────────────
            if (line.TrimStart().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    if (listTag != "") { sb.Append($"</{listTag}>"); listTag = ""; }
                    sb.Append("<pre><code>");
                    inCodeBlock = true;
                }
                else
                {
                    // trim trailing newline inside the code block
                    if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
                        sb.Length--;
                    sb.Append("</code></pre>");
                    inCodeBlock = false;
                }
                continue;
            }
            if (inCodeBlock)
            {
                sb.Append(System.Net.WebUtility.HtmlEncode(line)).Append('\n');
                continue;
            }

            // ── Blank line ────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(line))
            {
                if (listTag != "") { sb.Append($"</{listTag}>"); listTag = ""; }
                continue;
            }

            // ── Bullet list  (- item  or  * item) ─────────────────────────
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^[\-\*] "))
            {
                if (listTag != "ul") { if (listTag != "") sb.Append($"</{listTag}>"); sb.Append("<ul>"); listTag = "ul"; }
                sb.Append("<li>").Append(InlineMarkdown(line[2..])).Append("</li>");
                continue;
            }

            // ── Numbered list  (1. item) ───────────────────────────────────
            var numMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\d+\. (.+)");
            if (numMatch.Success)
            {
                if (listTag != "ol") { if (listTag != "") sb.Append($"</{listTag}>"); sb.Append("<ol>"); listTag = "ol"; }
                sb.Append("<li>").Append(InlineMarkdown(numMatch.Groups[1].Value)).Append("</li>");
                continue;
            }

            // Close any open list before non-list content
            if (listTag != "") { sb.Append($"</{listTag}>"); listTag = ""; }

            // ── Heading  (# / ## / ###) → bold paragraph ──────────────────
            var headingMatch = System.Text.RegularExpressions.Regex.Match(line, @"^#{1,3} (.+)");
            if (headingMatch.Success)
            {
                sb.Append("<p><strong>").Append(InlineMarkdown(headingMatch.Groups[1].Value)).Append("</strong></p>");
                continue;
            }

            // ── Normal paragraph ──────────────────────────────────────────
            sb.Append("<p>").Append(InlineMarkdown(line)).Append("</p>");
        }

        // Close anything still open
        if (listTag != "") sb.Append($"</{listTag}>");
        if (inCodeBlock) sb.Append("</code></pre>");

        return sb.ToString();
    }

    private static string InlineMarkdown(string text)
    {
        // **bold** → <strong>
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        // *italic* or _italic_ → <em>
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"_(.+?)_", "<em>$1</em>");
        // `code` → <code>
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`(.+?)`", "<code>$1</code>");
        return text;
    }

    public async Task<int> SelectBestAnswerAsync(string questionTitle, List<string> answers,
        CancellationToken cancellationToken = default)
    {
        if (answers.Count == 0) return -1;
        if (answers.Count == 1) return 0;

        var prompt = LlmPrompts.SelectBestAnswer(questionTitle, answers);
        var result = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);

        if (result != null && int.TryParse(result.Trim(), out int index) && index >= 0 && index < answers.Count)
        {
            return index;
        }

        // Fallback to random selection
        return Random.Shared.Next(0, answers.Count);
    }
}