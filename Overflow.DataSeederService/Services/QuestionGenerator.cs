using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Templates;
using System.Net.Http.Json;

namespace Overflow.DataSeederService.Services;

public class QuestionGenerator
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly LlmClient _llmClient;
    private readonly ILogger<QuestionGenerator> _logger;

    public QuestionGenerator(
        HttpClient httpClient,
        IOptions<SeederOptions> options,
        LlmClient llmClient,
        ILogger<QuestionGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<List<Tag>> GetAvailableTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_options.QuestionServiceUrl}/tags",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var tags = await response.Content.ReadFromJsonAsync<List<Tag>>(cancellationToken);
                return tags ?? new List<Tag>();
            }

            _logger.LogWarning("Failed to fetch tags: {StatusCode}", response.StatusCode);
            return new List<Tag>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tags");
            return new List<Tag>();
        }
    }

    public async Task<Question?> CreateQuestionAsync(
        string userId,
        string authToken,
        CancellationToken cancellationToken = default)
    {
        // Get available tags
        var tags = await GetAvailableTagsAsync(cancellationToken);
        if (tags.Count == 0)
        {
            _logger.LogWarning("No tags available, cannot create question");
            return null;
        }

        // Select 1-3 random tags
        var selectedTagCount = Random.Shared.Next(1, Math.Min(4, tags.Count + 1));
        var selectedTags = tags.OrderBy(_ => Random.Shared.Next())
            .Take(selectedTagCount)
            .Select(t => t.Slug)
            .ToList();

        var primaryTag = selectedTags.First();

        string title;
        string content;

        if (_options.EnableLlmGeneration)
        {
            var (llmTitle, llmContent) = await RunGenerationPipelineAsync(primaryTag, cancellationToken);

            if (!string.IsNullOrWhiteSpace(llmTitle) && !string.IsNullOrWhiteSpace(llmContent))
            {
                title = llmTitle;
                content = llmContent;
            }
            else
            {
                _logger.LogWarning("LLM pipeline failed for tag '{Tag}', falling back to static template", primaryTag);
                (title, content) = QuestionTemplates.GetRandomQuestion(primaryTag);
            }
        }
        else
        {
            _logger.LogInformation("LLM generation disabled, using template for tag: {Tag}", primaryTag);
            (title, content) = QuestionTemplates.GetRandomQuestion(primaryTag);
        }

        var questionDto = new CreateQuestionDto
        {
            Title = title,
            Content = content,
            Tags = selectedTags
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.QuestionServiceUrl}/questions");
            request.Headers.Add("Authorization", $"Bearer {authToken}");
            request.Content = JsonContent.Create(questionDto);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var question = await response.Content.ReadFromJsonAsync<Question>(cancellationToken);
                _logger.LogInformation("Created question: {Title} (ID: {Id})",
                    question?.Title, question?.Id);
                return question;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to create question: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating question");
            return null;
        }
    }

    /// <summary>
    /// Runs the 2-step LLM pipeline: TopicSeed → StructuredQuestion.
    /// Validates using <see cref="ContentAssembler.ValidateQuestion"/> and retries
    /// up to <see cref="SeederOptions.MaxGenerationRetries"/> times on validation failure.
    /// Returns (title, html-content) or (null, null) on failure.
    /// </summary>
    private async Task<(string? title, string? content)> RunGenerationPipelineAsync(
        string primaryTag, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Pipeline] Starting question generation for tag: {Tag}", primaryTag);

        // Step 1 — Topic Seed (runs once; provides the creative brief for Step 2)
        var seed = await _llmClient.GenerateTopicSeedAsync(primaryTag, cancellationToken);
        if (seed == null)
        {
            _logger.LogWarning("[Pipeline] Step 1 (TopicSeed) failed for tag: {Tag}", primaryTag);
            return (null, null);
        }

        // Step 2 — Structured Question with validation + retry
        int maxAttempts = _options.MaxGenerationRetries + 1;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var dto = await _llmClient.GenerateStructuredQuestionAsync(seed, cancellationToken);

            var issues = ContentAssembler.ValidateQuestion(dto);
            if (issues.Count > 0)
            {
                _logger.LogWarning(
                    "[Pipeline] Attempt {A}/{Max}: validation failed — {Issues}",
                    attempt, maxAttempts, string.Join("; ", issues));
                continue;
            }

            var cleanTitle = System.Text.RegularExpressions.Regex
                .Replace(dto!.Title, "<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                _logger.LogWarning("[Pipeline] Attempt {A}/{Max}: title empty after strip", attempt, maxAttempts);
                continue;
            }

            var htmlContent = ContentAssembler.BuildQuestionHtml(dto, _logger);

            if (!ContentAssembler.ValidateRenderedQuestion(htmlContent, dto, out var renderError))
            {
                _logger.LogWarning("[Pipeline] Attempt {A}/{Max}: render validation failed — {Error}",
                    attempt, maxAttempts, renderError);
                continue;
            }

            _logger.LogInformation("[Pipeline] Question ready (attempt {A}): '{Title}' ({Len} chars HTML)",
                attempt, cleanTitle, htmlContent.Length);
            return (cleanTitle, htmlContent);
        }

        _logger.LogWarning("[Pipeline] All {Max} attempts failed for tag: {Tag}", maxAttempts, primaryTag);
        return (null, null);
    }
}
