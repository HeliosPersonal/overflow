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

        return (title, body);
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
        return await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);
    }

    public async Task<string?> GenerateAnswerAsync(string questionTitle, string questionContent,
        CancellationToken cancellationToken = default)
    {
        var prompt = LlmPrompts.Answer(questionTitle, questionContent);
        return await GenerateAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken,
            maxTokens: prompt.MaxTokens, temperature: prompt.Temperature);
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