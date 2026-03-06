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

        if (_options.EnableLlmGeneration)
        {
            answerContent = await RunAnswerPipelineAsync(questionTitle, questionContent, cancellationToken)
                            ?? AnswerTemplates.GetRandomAnswer();
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

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to create answer: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating answer");
            return null;
        }
    }

    /// <summary>
    /// Runs the 3-step answer pipeline: StructuredAnswer → Critic → Repair (if needed).
    /// Returns HTML content, or null if the pipeline fails entirely.
    /// </summary>
    private async Task<string?> RunAnswerPipelineAsync(
        string questionTitle, string questionContent, CancellationToken cancellationToken)
    {
        // Reconstruct a lightweight QuestionGenerationDto from stored content.
        // The stored content is HTML — pass it as Context so the LLM has question context.
        var questionDto = new QuestionGenerationDto
        {
            Title = questionTitle,
            Context = questionContent
        };

        // Step 3 — Generate structured answer with validation + retry
        AnswerGenerationDto? answerDto = null;
        int maxAttempts = _options.MaxGenerationRetries + 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var candidate = await _llmClient.GenerateStructuredAnswerAsync(questionDto, cancellationToken);
            var issues = ContentAssembler.ValidateAnswer(candidate);

            if (issues.Count == 0)
            {
                answerDto = candidate;
                break;
            }

            _logger.LogWarning(
                "[AnswerPipeline] Attempt {A}/{Max}: validation failed — {Issues}",
                attempt, maxAttempts, string.Join("; ", issues));
        }

        if (answerDto == null)
        {
            _logger.LogWarning("[AnswerPipeline] All {Max} attempts failed for: '{Title}'",
                maxAttempts, questionTitle);
            return null;
        }

        // Step 4 — Critic evaluation (optional, controlled by configuration)
        if (_options.EnableCriticPass)
        {
            var criticResult = await _llmClient.CriticEvaluateAsync(questionDto, answerDto, cancellationToken);

            if (criticResult != null && !criticResult.Valid && criticResult.Issues.Count > 0)
            {
                _logger.LogInformation(
                    "[AnswerPipeline] Critic flagged {IssueCount} issue(s): {Issues}",
                    criticResult.Issues.Count, string.Join("; ", criticResult.Issues));

                // Step 5 — Repair pass (optional)
                if (_options.EnableRepairPass)
                {
                    var repaired = await _llmClient.RepairAsync(questionDto, answerDto, criticResult, cancellationToken);

                    if (repaired?.Answer != null &&
                        ContentAssembler.ValidateAnswer(repaired.Answer).Count == 0)
                    {
                        _logger.LogInformation("[AnswerPipeline] Using repaired answer");
                        answerDto = repaired.Answer;
                    }
                    else
                    {
                        _logger.LogWarning("[AnswerPipeline] Repair result invalid or null, using original");
                    }
                }
            }
            else if (criticResult != null)
            {
                _logger.LogInformation("[AnswerPipeline] Critic: answer passed validation");
            }
            else
            {
                _logger.LogWarning("[AnswerPipeline] Critic evaluation failed, using original answer");
            }
        }

        // Assemble final HTML via ContentAssembler — deterministic layout
        var html = ContentAssembler.BuildAnswerHtml(answerDto, _logger);

        if (!ContentAssembler.ValidateRenderedAnswer(html, answerDto, out var renderError))
        {
            _logger.LogWarning("[AnswerPipeline] Render validation failed: {Error} — falling back to template", renderError);
            return null;
        }

        return html;
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
