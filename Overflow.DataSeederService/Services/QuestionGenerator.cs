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

        // Try to generate with LLM first — unified call ensures title+body are about the same topic
        if (_options.EnableLlmGeneration)
        {
            _logger.LogInformation("Attempting unified LLM generation (title+body) for tag: {Tag}", primaryTag);
            var variability = ContentVariability.RandomForQuestion();
            var genStart = DateTime.UtcNow;
            
            var (llmTitle, llmContent) = await _llmClient.GenerateQuestionTitleAndContentAsync(
                primaryTag, variability, cancellationToken);
            var genElapsed = (DateTime.UtcNow - genStart).TotalSeconds;
            
            if (!string.IsNullOrWhiteSpace(llmTitle) && !string.IsNullOrWhiteSpace(llmContent))
            {
                _logger.LogInformation("LLM generated title+body in {Elapsed}s: '{Title}' ({BodyLength} chars)", 
                    genElapsed, llmTitle, llmContent.Length);
                title = llmTitle;
                content = llmContent;
            }
            else
            {
                _logger.LogWarning("Unified LLM generation failed after {Elapsed}s, falling back to separate calls", 
                    genElapsed);
                
                // Fallback: try separate title + content generation
                var titleStart = DateTime.UtcNow;
                var fallbackTitle = await _llmClient.GenerateQuestionTitleAsync(primaryTag, cancellationToken);
                var titleElapsed = (DateTime.UtcNow - titleStart).TotalSeconds;
                
                if (!string.IsNullOrWhiteSpace(fallbackTitle))
                {
                    _logger.LogInformation("Fallback LLM title generated in {Elapsed}s: '{Title}'", titleElapsed, fallbackTitle);
                    title = fallbackTitle;
                    
                    var contentStart = DateTime.UtcNow;
                    var fallbackContent = await _llmClient.GenerateQuestionContentAsync(title, primaryTag, cancellationToken);
                    var contentElapsed = (DateTime.UtcNow - contentStart).TotalSeconds;
                    
                    if (!string.IsNullOrWhiteSpace(fallbackContent))
                    {
                        _logger.LogInformation("Fallback LLM content generated in {Elapsed}s ({Length} chars)", 
                            contentElapsed, fallbackContent.Length);
                        content = fallbackContent;
                    }
                    else
                    {
                        _logger.LogWarning("Fallback LLM content generation failed after {Elapsed}s, using template", 
                            contentElapsed);
                        content = QuestionTemplates.GetRandomQuestion(primaryTag).content;
                    }
                }
                else
                {
                    _logger.LogWarning("Fallback LLM title generation failed after {Elapsed}s, using template", 
                        titleElapsed);
                    (title, content) = QuestionTemplates.GetRandomQuestion(primaryTag);
                }
            }
        }
        else
        {
            _logger.LogInformation("LLM generation disabled, using templates for tag: {Tag}", primaryTag);
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
