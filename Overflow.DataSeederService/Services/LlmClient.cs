using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Templates;

namespace Overflow.DataSeederService.Services;

public class LlmClient
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly ILogger<LlmClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public LlmClient(HttpClient httpClient, IOptions<SeederOptions> options, ILogger<LlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "LlmClient initialized - URL: {Url}, Model: {Model}, Enabled: {Enabled}, HttpClient Timeout: {Timeout}s",
            _options.LlmApiUrl, _options.LlmModel, _options.EnableLlmGeneration, _httpClient.Timeout.TotalSeconds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Core raw-text generation
    // ─────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────
    //  Generic structured JSON generation with retry
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls the LLM and attempts to deserialize the response as <typeparamref name="T"/>.
    /// Retries up to <see cref="SeederOptions.MaxGenerationRetries"/> times on JSON parse failure.
    /// </summary>
    private async Task<T?> GenerateStructuredAsync<T>(LlmPrompt prompt, string stepName,
        CancellationToken cancellationToken = default) where T : class
    {
        int maxAttempts = _options.MaxGenerationRetries + 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var raw = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
                prompt.MaxTokens, prompt.Temperature);

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning("[{Step}] Attempt {Attempt}/{Max}: LLM returned empty response",
                    stepName, attempt, maxAttempts);
                continue;
            }

            var json = ExtractJson(raw);

            try
            {
                var dto = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (dto != null)
                {
                    _logger.LogDebug("[{Step}] Attempt {Attempt}/{Max}: Successfully parsed JSON ({Len} chars)",
                        stepName, attempt, maxAttempts, json.Length);
                    return dto;
                }

                _logger.LogWarning("[{Step}] Attempt {Attempt}/{Max}: Deserialized to null. Raw: {Raw}",
                    stepName, attempt, maxAttempts, raw.Substring(0, Math.Min(200, raw.Length)));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[{Step}] Attempt {Attempt}/{Max}: JSON parse failed. Raw: {Raw}",
                    stepName, attempt, maxAttempts, raw.Substring(0, Math.Min(200, raw.Length)));
            }
        }

        _logger.LogError("[{Step}] All {Max} attempts failed to produce valid JSON", stepName, maxAttempts);
        return null;
    }

    /// <summary>
    /// Strips markdown code fences and extracts the first JSON object or array from the text.
    /// </summary>
    private static string ExtractJson(string raw)
    {
        raw = raw.Trim();

        // Strip ```json ... ``` or ``` ... ``` wrappers
        var fenceMatch = System.Text.RegularExpressions.Regex.Match(
            raw, @"^```[a-zA-Z]*\r?\n([\s\S]*?)```\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        if (fenceMatch.Success)
            raw = fenceMatch.Groups[1].Value.Trim();

        // Find first { or [ and last matching } or ]
        var objStart = raw.IndexOf('{');
        var arrStart = raw.IndexOf('[');

        if (objStart < 0 && arrStart < 0) return raw;

        int start;
        char close;
        if (arrStart >= 0 && (objStart < 0 || arrStart < objStart))
        {
            start = arrStart; close = ']';
        }
        else
        {
            start = objStart; close = '}';
        }

        var end = raw.LastIndexOf(close);
        if (end > start)
            return raw.Substring(start, end - start + 1);

        return raw;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pipeline Step 1 — Topic Seed
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a structured technical problem seed for the given tag.
    /// </summary>
    public async Task<TopicSeedDto?> GenerateTopicSeedAsync(string tag,
        CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.TopicSeed(tag);
        var result = await GenerateStructuredAsync<TopicSeedDto>(prompt, "TopicSeed", cancellationToken);

        if (result != null)
            _logger.LogInformation("[TopicSeed] Generated seed: topic={Topic}, difficulty={Difficulty}, type={Type}",
                result.Topic, result.Difficulty, result.ProblemType);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pipeline Step 2 — Structured Question
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a structured StackOverflow-style question from a topic seed.
    /// </summary>
    public async Task<QuestionGenerationDto?> GenerateStructuredQuestionAsync(TopicSeedDto topic,
        CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.StructuredQuestion(topic);
        var result = await GenerateStructuredAsync<QuestionGenerationDto>(prompt, "StructuredQuestion", cancellationToken);

        if (result != null)
            _logger.LogInformation("[StructuredQuestion] Generated: title='{Title}', lang={Lang}, context={Len}chars, tags=[{Tags}]",
                result.Title, result.Language, result.Context.Length, string.Join(", ", result.Tags));

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pipeline Step 3 — Structured Answer
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a structured StackOverflow-style answer for the given question.
    /// </summary>
    public async Task<AnswerGenerationDto?> GenerateStructuredAnswerAsync(QuestionGenerationDto question,
        CancellationToken cancellationToken = default)
    {
        var style = ContentVariability.RandomForAnswer().Style;
        var prompt = LlmPrompts.StructuredAnswer(question, style);
        var result = await GenerateStructuredAsync<AnswerGenerationDto>(prompt, "StructuredAnswer", cancellationToken);

        if (result != null)
            _logger.LogInformation("[StructuredAnswer] Generated: style={Style}, lang={Lang}, explanation={Len}chars, hasCode={HasCode}",
                style, result.Language, result.Explanation.Length, !string.IsNullOrWhiteSpace(result.CodeSnippet));

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pipeline Step 4 — Critic Evaluation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates quality of a question+answer pair and returns a critic result.
    /// </summary>
    public async Task<CriticResultDto?> CriticEvaluateAsync(QuestionGenerationDto question,
        AnswerGenerationDto answer, CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.Critic(question, answer);
        var result = await GenerateStructuredAsync<CriticResultDto>(prompt, "Critic", cancellationToken);

        if (result != null)
            _logger.LogInformation("[Critic] Evaluation: valid={Valid}, issues={IssueCount} ({Issues})",
                result.Valid, result.Issues.Count, string.Join("; ", result.Issues));

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pipeline Step 5 — Repair Pass
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Repairs question and answer based on critic feedback.
    /// </summary>
    public async Task<RepairResultDto?> RepairAsync(QuestionGenerationDto question,
        AnswerGenerationDto answer, CriticResultDto critic, CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.Repair(question, answer, critic);
        var result = await GenerateStructuredAsync<RepairResultDto>(prompt, "Repair", cancellationToken);

        if (result != null)
            _logger.LogInformation("[Repair] Repaired: question={HasQ}, answer={HasA}",
                result.Question != null, result.Answer != null);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Best Answer Selection
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<int> SelectBestAnswerAsync(string questionTitle, List<string> answers,
        CancellationToken cancellationToken = default)
    {
        if (answers.Count == 0) return -1;
        if (answers.Count == 1) return 0;

#pragma warning disable CS0618
        var prompt = LlmPrompts.SelectBestAnswer(questionTitle, answers);
#pragma warning restore CS0618
        var result = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);

        if (result != null && int.TryParse(result.Trim(), out int index) && index >= 0 && index < answers.Count)
        {
            return index;
        }

        // Fallback to random selection
        return Random.Shared.Next(0, answers.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Legacy methods (kept for backwards compatibility)
    // ─────────────────────────────────────────────────────────────────────────

    [Obsolete("Use GenerateTopicSeedAsync + GenerateStructuredQuestionAsync instead.")]
    public async Task<(string? title, string? content)> GenerateQuestionTitleAndContentAsync(
        string tag, ContentVariability variability, CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.TopicSeed(tag); // best-effort fallback to topic seed
        var result = await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);
        if (string.IsNullOrWhiteSpace(result)) return (null, null);
        var lines = result.Split('\n', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length >= 2 ? (lines[0], SanitizeHtml(lines[1])) : (null, null);
    }

    [Obsolete("Use GenerateStructuredAnswerAsync instead.")]
    public async Task<string?> GenerateAnswerAsync(string questionTitle, string questionContent,
        CancellationToken cancellationToken = default)
    {
        var stub = new QuestionGenerationDto { Title = questionTitle, Context = questionContent };
        var dto = await GenerateStructuredAnswerAsync(stub, cancellationToken);
        if (dto == null) return null;
        return ContentAssembler.BuildAnswerHtml(dto);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HTML/Markdown utilities (public for use in generators)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts Markdown text to HTML. Handles fenced code blocks, lists, headings, inline styles.
    /// </summary>
    public static string SanitizeHtml(string content)
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
}