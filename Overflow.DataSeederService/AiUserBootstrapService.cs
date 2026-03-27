using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService;

public class AiUserBootstrapService(
    AiUserProvider aiUserProvider,
    IOptions<AiAnswerOptions> options,
    ILogger<AiUserBootstrapService> logger) : IHostedService
{
    private const int MaxRetries = 3;

    public async Task StartAsync(CancellationToken ct)
    {
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.AiEmail))
        {
            logger.LogCritical("AiAnswerOptions:AiEmail is empty — AI answers disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(opts.AiPassword))
        {
            logger.LogCritical("AiAnswerOptions:AiPassword is empty — AI answers disabled");
            return;
        }

        logger.LogInformation("Bootstrapping AI user '{Email}'", opts.AiEmail);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var result = await TryBootstrapAsync(attempt, ct);
            if (result)
            {
                return;
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task<bool> TryBootstrapAsync(int attempt, CancellationToken ct)
    {
        try
        {
            var result = await aiUserProvider.BootstrapAsync(ct);
            if (result.IsSuccess)
            {
                logger.LogInformation("Bootstrap completed — AI user ready");
                return true;
            }

            LogRetryOrGiveUp(attempt, result.Error);
        }
        catch (OperationCanceledException)
        {
            return true; // shutting down
        }
        catch (Exception ex)
        {
            LogRetryOrGiveUp(attempt, ex.Message);
        }

        if (attempt < MaxRetries)
        {
            await DelayBeforeRetryAsync(attempt, ct);
        }

        return false;
    }

    private void LogRetryOrGiveUp(int attempt, string error)
    {
        if (attempt < MaxRetries)
        {
            logger.LogWarning("Bootstrap attempt {A}/{Max} failed: {Error}", attempt, MaxRetries, error);
        }
        else
        {
            logger.LogError("Bootstrap failed after {Max} attempts: {Error} — will retry lazily", MaxRetries, error);
        }
    }

    private static async Task DelayBeforeRetryAsync(int attempt, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
        catch (OperationCanceledException)
        {
            // shutting down — swallow
        }
    }
}