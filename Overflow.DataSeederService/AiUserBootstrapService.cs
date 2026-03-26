using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService;

/// <summary>
///     Hosted service that runs on startup to:
///     1. Pull the LLM model if not already available in Ollama
///     2. Ensure the AI user account exists in Keycloak with a profile
///     Retries with exponential backoff.
/// </summary>
public class AiUserBootstrapService(
    AiUserProvider aiUserProvider,
    IOllamaApiClient ollama,
    IOptions<AiAnswerOptions> options,
    ILogger<AiUserBootstrapService> logger) : IHostedService
{
    private const int MaxRetries = 5;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("AiUserBootstrapService starting — ensuring AI user exists");

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await EnsureModelAvailableAsync(cancellationToken);
                await aiUserProvider.BootstrapAsync(cancellationToken);
                logger.LogInformation("Bootstrap completed — AI user and LLM model ready");
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(ex,
                    "Bootstrap attempt {Attempt}/{Max} failed — retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Bootstrap failed after {Max} attempts — AI answers will not be posted",
                    MaxRetries);
                throw;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureModelAvailableAsync(CancellationToken ct)
    {
        var model = options.Value.LlmModel;

        logger.LogInformation("Checking if model '{Model}' is available in Ollama...", model);

        var localModels = await ollama.ListLocalModelsAsync(ct);
        if (localModels.Any(m => m.Name.StartsWith(model, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogInformation("Model '{Model}' already available", model);
            return;
        }

        logger.LogInformation("Model '{Model}' not found — pulling (this may take several minutes)...", model);

        await foreach (var status in ollama.PullModelAsync(new PullModelRequest { Model = model }, ct))
        {
            if (!string.IsNullOrWhiteSpace(status?.Status))
                logger.LogInformation("[Pull] {Status}", status.Status);
        }

        logger.LogInformation("Model '{Model}' pulled successfully", model);
    }
}