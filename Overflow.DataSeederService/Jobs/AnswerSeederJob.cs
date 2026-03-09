using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService.Jobs;

/// <summary>
///     Answers the newest question every <see cref="SeederOptions.AnswerIntervalMinutes" /> min. Never answers own
///     question.
/// </summary>
public class PostAnswerJob(
    SeederUserPool userPool,
    QuestionService questionService,
    AnswerService answerService,
    IOptions<SeederOptions> options,
    ILogger<PostAnswerJob> logger)
    : BackgroundService
{
    private readonly SeederOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("PostAnswerJob starting — interval: {Minutes} min",
            _options.AnswerIntervalMinutes);

        await Task.Delay(TimeSpan.FromMinutes(_options.AnswerStartDelayMinutes), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PostAnswerJob failed unexpectedly");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.AnswerIntervalMinutes), ct);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var pool = await userPool.GetAsync(ct);
        if (pool.Count < _options.MinAnswerPoolSize)
        {
            logger.LogWarning("PostAnswerJob: need at least 2 users in pool, skipping");
            return;
        }

        var question = await questionService.GetNewestQuestionAsync(ct);
        if (question == null)
        {
            logger.LogInformation("PostAnswerJob: no questions exist yet, skipping");
            return;
        }

        var candidates = pool.Where(u => u.KeycloakUserId != question.AskerId).ToList();
        if (candidates.Count == 0)
        {
            logger.LogWarning("PostAnswerJob: all pool users are the question author, skipping");
            return;
        }

        var author = candidates[Random.Shared.Next(candidates.Count)];
        var token = await userPool.RefreshTokenAsync(author, ct);
        var complexity = ComplexityLevelExtensions.Random();

        if (token == null)
        {
            logger.LogWarning("PostAnswerJob: could not refresh token for {User}, skipping", author.DisplayName);
            return;
        }

        logger.LogInformation("PostAnswerJob: answering '{Title}' as '{User}', complexity={Complexity}",
            question.Title, author.DisplayName, complexity);

        var answer = await answerService.PostAnswerAsync(question, author, complexity, ct);
        if (answer != null)
        {
            logger.LogInformation("PostAnswerJob: done — answer {Id} posted", answer.Id);
        }
        else
        {
            logger.LogWarning("PostAnswerJob: answer was not created this run");
        }
    }
}