using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService.Jobs;

/// <summary>
///     Accepts the best answer on up to 3 unaccepted questions every
///     <see cref="SeederOptions.AcceptIntervalMinutes" /> min.
/// </summary>
public class AcceptBestAnswerJob(
    SeederUserPool userPool,
    QuestionService questionService,
    AnswerService answerService,
    LlmService llm,
    IOptions<SeederOptions> options,
    ILogger<AcceptBestAnswerJob> logger)
    : BackgroundService
{
    private readonly SeederOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("AcceptBestAnswerJob starting — interval: {Minutes} min",
            _options.AcceptIntervalMinutes);

        await Task.Delay(TimeSpan.FromMinutes(_options.AcceptStartDelayMinutes), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AcceptBestAnswerJob failed unexpectedly");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.AcceptIntervalMinutes), ct);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var pool = await userPool.GetAsync(ct);

        var questions = await questionService.GetUnacceptedQuestionsWithAnswersAsync(_options.AcceptQuestionsPerRun, ct);
        if (questions.Count == 0)
        {
            logger.LogInformation("AcceptBestAnswerJob: no unaccepted questions with answers found");
            return;
        }

        logger.LogInformation("AcceptBestAnswerJob: processing {Count} question(s)", questions.Count);

        var accepted = 0;
        foreach (var question in questions)
        {
            var asker = pool.FirstOrDefault(u => u.KeycloakUserId == question.AskerId);
            if (asker == null)
            {
                logger.LogDebug("AcceptBestAnswerJob: '{Title}' was not asked by a seeder user, skipping",
                    question.Title);
                continue;
            }

            var token = await userPool.RefreshTokenAsync(asker, ct);
            if (token == null)
            {
                logger.LogWarning("AcceptBestAnswerJob: could not refresh token for {User}, skipping",
                    asker.DisplayName);
                continue;
            }

            var bestIndex = question.Answers.Count > 1
                ? await llm.SelectBestAnswerIndexAsync(
                    question.Title,
                    question.Answers.Select(a => a.Content).ToList(), ct)
                : 0;

            var best = question.Answers[bestIndex];

            logger.LogInformation("AcceptBestAnswerJob: accepting answer {AnswerId} on '{Title}'",
                best.Id, question.Title);

            if (await answerService.AcceptAnswerAsync(question.Id, best.Id, asker, ct))
            {
                accepted++;
            }

            await Task.Delay(Random.Shared.Next(_options.AcceptDelayMinMs, _options.AcceptDelayMaxMs), ct);
        }

        logger.LogInformation("AcceptBestAnswerJob: done — accepted {Accepted}/{Total}", accepted, questions.Count);
    }
}