using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Templates;
using System.Net.Http.Json;

namespace Overflow.DataSeederService.Services;

public class AnswerGenerator
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly LlmClient _llmClient;
    private readonly ILogger<AnswerGenerator> _logger;

    public AnswerGenerator(
        HttpClient httpClient,
        IOptions<SeederOptions> options,
        LlmClient llmClient,
        ILogger<AnswerGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<Answer?> CreateAnswerAsync(
        string questionId,
        string questionTitle,
        string questionContent,
        string userId,
        string authToken,
        CancellationToken cancellationToken = default)
    {
        string answerContent;

        // Try to generate with LLM first
        if (_options.EnableLlmGeneration)
        {
            var llmAnswer = await _llmClient.GenerateAnswerAsync(
                questionTitle, 
                questionContent, 
                cancellationToken);
            
            answerContent = llmAnswer ?? AnswerTemplates.GetRandomAnswer();
        }
        else
        {
            answerContent = AnswerTemplates.GetRandomAnswer();
        }

        var answerDto = new CreateAnswerDto
        {
            Content = answerContent
        };

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post, 
                $"{_options.QuestionServiceUrl}/questions/{questionId}/answers");
            request.Headers.Add("Authorization", $"Bearer {authToken}");
            request.Content = JsonContent.Create(answerDto);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var answer = await response.Content.ReadFromJsonAsync<Answer>(cancellationToken);
                _logger.LogInformation("Created answer for question {QuestionId} (Answer ID: {Id})", 
                    questionId, answer?.Id);
                return answer;
            }

            _logger.LogWarning("Failed to create answer: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating answer");
            return null;
        }
    }

    public async Task<List<Answer>> CreateMultipleAnswersAsync(
        Question question,
        List<(string userId, string token)> answerers,
        CancellationToken cancellationToken = default)
    {
        var answers = new List<Answer>();
        var answerCount = Random.Shared.Next(
            _options.MinAnswersPerQuestion, 
            _options.MaxAnswersPerQuestion + 1);

        // Ensure we don't try to create more answers than we have users
        answerCount = Math.Min(answerCount, answerers.Count);

        for (int i = 0; i < answerCount; i++)
        {
            var (userId, token) = answerers[i];
            
            var answer = await CreateAnswerAsync(
                question.Id,
                question.Title,
                question.Content,
                userId,
                token,
                cancellationToken);

            if (answer != null)
            {
                answers.Add(answer);
                // Realistic delay between answers
                await Task.Delay(
                    Random.Shared.Next(2000, 10000), 
                    cancellationToken);
            }
        }

        return answers;
    }

    public async Task<bool> AcceptAnswerAsync(
        string questionId,
        string answerId,
        string askerAuthToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.QuestionServiceUrl}/questions/{questionId}/answers/{answerId}/accept");
            request.Headers.Add("Authorization", $"Bearer {askerAuthToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Accepted answer {AnswerId} for question {QuestionId}", 
                    answerId, questionId);
                return true;
            }

            _logger.LogWarning("Failed to accept answer: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting answer");
            return false;
        }
    }
}
