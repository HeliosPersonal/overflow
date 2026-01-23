using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;

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
        
        _logger.LogInformation("LlmClient initialized - URL: {Url}, Model: {Model}, Enabled: {Enabled}, HttpClient Timeout: {Timeout}s",
            _options.LlmApiUrl, _options.LlmModel, _options.EnableLlmGeneration, _httpClient.Timeout.TotalSeconds);
    }

    public async Task<string?> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
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
            temperature = 0.7,
            max_tokens = 1000
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
            _logger.LogInformation("LLM API responded in {Elapsed}s with status {StatusCode}", elapsed, response.StatusCode);
            
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
            _logger.LogError(ex, "UNEXPECTED ERROR generating content with LLM after {Elapsed}s. Type: {ExceptionType}, Message: {Message}", 
                elapsed, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    public async Task<string?> GenerateQuestionTitleAsync(string tag, CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a developer asking a technical question on a Stack Overflow-like platform. Generate only the question title, nothing else.";
        var userPrompt = $"Generate a realistic Stack Overflow question title about {tag}. The title should be clear, specific, and between 15-100 characters. Return ONLY the title text, no quotes or extra formatting.";
        
        return await GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string?> GenerateQuestionContentAsync(string title, string tag, CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a developer asking a technical question. Write a clear, detailed question body with code examples if relevant. Use markdown formatting.";
        var userPrompt = $"Write the question body for this title: '{title}'. Topic: {tag}. Include context, what you've tried, and specific details. Keep it between 100-500 words.";
        
        return await GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string?> GenerateAnswerAsync(string questionTitle, string questionContent, CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are an experienced developer providing a helpful answer to a technical question. Be concise, accurate, and include code examples when relevant.";
        var userPrompt = $"Answer this question:\n\nTitle: {questionTitle}\n\nContent: {questionContent}\n\nProvide a clear, helpful answer with code examples if appropriate. Keep it between 50-400 words.";
        
        return await GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<int> SelectBestAnswerAsync(string questionTitle, List<string> answers, CancellationToken cancellationToken = default)
    {
        if (answers.Count == 0) return -1;
        if (answers.Count == 1) return 0;

        var answersText = string.Join("\n\n---\n\n", answers.Select((a, i) => $"Answer {i}: {a}"));
        var systemPrompt = "You are evaluating technical answers for quality and helpfulness.";
        var userPrompt = $"Question: {questionTitle}\n\nAnswers:\n{answersText}\n\nWhich answer index (0-{answers.Count - 1}) is most helpful and accurate? Respond with ONLY the number.";
        
        var result = await GenerateAsync(systemPrompt, userPrompt, cancellationToken);
        
        if (result != null && int.TryParse(result.Trim(), out int index) && index >= 0 && index < answers.Count)
        {
            return index;
        }

        // Fallback to random selection
        return Random.Shared.Next(0, answers.Count);
    }
}
