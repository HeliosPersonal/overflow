using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService.Jobs;

/// <summary>
///     Posts one question every <see cref="SeederOptions.QuestionIntervalMinutes" /> min as a random seeder user.
/// </summary>
public class PostQuestionJob(
    SeederUserPool userPool,
    QuestionService questionService,
    IOptions<SeederOptions> options,
    ILogger<PostQuestionJob> logger)
    : BackgroundService
{
    private readonly SeederOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("PostQuestionJob starting — interval: {Minutes} min",
            _options.QuestionIntervalMinutes);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PostQuestionJob failed unexpectedly");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.QuestionIntervalMinutes), ct);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var pool = await userPool.GetAsync(ct);
        if (pool.Count == 0)
        {
            logger.LogWarning("PostQuestionJob: user pool is empty, skipping");
            return;
        }

        var author = pool[Random.Shared.Next(pool.Count)];
        var token = await userPool.RefreshTokenAsync(author, ct);
        if (token == null)
        {
            logger.LogWarning("PostQuestionJob: could not refresh token for {User}, skipping", author.DisplayName);
            return;
        }

        var complexity = ComplexityLevelExtensions.Random();
        logger.LogInformation("PostQuestionJob: posting question as '{User}', complexity={Complexity}",
            author.DisplayName, complexity);

        var question = await questionService.PostQuestionAsync(author, complexity, ct);
        if (question != null)
        {
            logger.LogInformation("PostQuestionJob: done — '{Title}' (ID: {Id})", question.Title, question.Id);
        }
        else
        {
            logger.LogWarning("PostQuestionJob: question was not created this run");
        }
    }
}