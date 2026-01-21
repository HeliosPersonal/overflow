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

        // Try to generate with LLM first
        if (_options.EnableLlmGeneration)
        {
            var llmTitle = await _llmClient.GenerateQuestionTitleAsync(primaryTag, cancellationToken);
            if (!string.IsNullOrWhiteSpace(llmTitle))
            {
                title = llmTitle;
                var llmContent = await _llmClient.GenerateQuestionContentAsync(title, primaryTag, cancellationToken);
                content = llmContent ?? QuestionTemplates.GetRandomQuestion(primaryTag).content;
            }
            else
            {
                // Fallback to templates
                (title, content) = QuestionTemplates.GetRandomQuestion(primaryTag);
            }
        }
        else
        {
            // Use templates
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
}
