using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public class SeederBackgroundService : BackgroundService
{
    private readonly ILogger<SeederBackgroundService> _logger;
    private readonly SeederOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public SeederBackgroundService(
        ILogger<SeederBackgroundService> logger,
        IOptions<SeederOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Seeder Service starting...");
        _logger.LogInformation("Seeding interval: {Interval} minutes", _options.IntervalMinutes);
        _logger.LogInformation("LLM Generation: {Enabled}", _options.EnableLlmGeneration ? "Enabled" : "Disabled");
        
        // Wait a bit before starting to ensure services are up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SeedDataAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during seeding operation");
            }

            // Wait for the configured interval
            var delay = TimeSpan.FromMinutes(_options.IntervalMinutes);
            _logger.LogInformation("Next seeding run in {Minutes} minutes", _options.IntervalMinutes);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task SeedDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting data seeding operation ===");

        using var scope = _serviceProvider.CreateScope();
        
        var userGenerator = scope.ServiceProvider.GetRequiredService<UserGenerator>();
        var questionGenerator = scope.ServiceProvider.GetRequiredService<QuestionGenerator>();
        var answerGenerator = scope.ServiceProvider.GetRequiredService<AnswerGenerator>();
        var votingService = scope.ServiceProvider.GetRequiredService<VotingService>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
        var llmClient = scope.ServiceProvider.GetRequiredService<LlmClient>();

        // Step 1: Smart user pool management (max 1000 users)
        _logger.LogInformation("Step 1: Managing user pool (max 1000 users)...");
        var userPool = await userGenerator.GetOrCreateUserPoolAsync(cancellationToken);
        
        _logger.LogInformation("User pool size: {Count}/1000", userPool.Count);
        
        var allUsers = userPool;

        if (allUsers.Count < 3)
        {
            _logger.LogWarning("Not enough users to create realistic data. Need at least 3 users.");
            return;
        }

        _logger.LogInformation("Total available users: {Count}", allUsers.Count);

        // Step 2: Select a random user to ask a question
        var asker = allUsers[Random.Shared.Next(allUsers.Count)];
        _logger.LogInformation("Step 2: User '{DisplayName}' will ask a question", asker.Profile.DisplayName);

        // Ensure we have a valid token (refresh if needed)
        if (string.IsNullOrEmpty(asker.Token) && !string.IsNullOrEmpty(asker.KeycloakUserId))
        {
            asker.Token = await authService.GetUserTokenAsync(asker.KeycloakUserId, cancellationToken);
        }

        if (asker.Token == null)
        {
            _logger.LogWarning("Could not get authentication token for asker");
            return;
        }

        // Step 3: Create a question
        _logger.LogInformation("Step 3: Generating question...");
        var question = await questionGenerator.CreateQuestionAsync(
            asker.KeycloakUserId ?? asker.Profile.Id,
            asker.Token,
            cancellationToken);

        if (question == null)
        {
            _logger.LogWarning("Failed to create question");
            return;
        }

        _logger.LogInformation("Created question: '{Title}'", question.Title);

        // Step 4: Wait a bit (realistic delay before answers come in)
        var delaySeconds = Random.Shared.Next(5, 15);
        _logger.LogInformation("Waiting {Seconds} seconds before generating answers...", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

        // Step 5: Select different users to answer (excluding the asker)
        var potentialAnswerers = allUsers.Where(u => u.Profile.Id != asker.Profile.Id).ToList();
        if (potentialAnswerers.Count == 0)
        {
            _logger.LogWarning("No users available to answer questions");
            return;
        }

        var answerCount = Random.Shared.Next(_options.MinAnswersPerQuestion, _options.MaxAnswersPerQuestion + 1);
        answerCount = Math.Min(answerCount, potentialAnswerers.Count);

        var answerers = potentialAnswerers
            .OrderBy(_ => Random.Shared.Next())
            .Take(answerCount)
            .ToList();

        _logger.LogInformation("Step 5: Generating {Count} answers from different users...", answerCount);

        var answerersWithTokens = new List<(string userId, string token)>();
        foreach (var answerer in answerers)
        {
            // Ensure we have a valid token
            if (string.IsNullOrEmpty(answerer.Token) && !string.IsNullOrEmpty(answerer.KeycloakUserId))
            {
                answerer.Token = await authService.GetUserTokenAsync(answerer.KeycloakUserId, cancellationToken);
            }

            if (answerer.Token != null)
            {
                answerersWithTokens.Add((answerer.KeycloakUserId ?? answerer.Profile.Id, answerer.Token));
            }
        }

        var answers = await answerGenerator.CreateMultipleAnswersAsync(
            question,
            answerersWithTokens,
            cancellationToken);

        if (answers.Count == 0)
        {
            _logger.LogWarning("No answers were created");
            return;
        }

        _logger.LogInformation("Created {Count} answers", answers.Count);

        // Step 6: Determine which answer to accept
        _logger.LogInformation("Step 6: Determining best answer to accept...");
        
        int bestAnswerIndex;
        if (_options.EnableLlmGeneration && answers.Count > 1)
        {
            var answerContents = answers.Select(a => a.Content).ToList();
            bestAnswerIndex = await llmClient.SelectBestAnswerAsync(
                question.Title,
                answerContents,
                cancellationToken);
        }
        else
        {
            bestAnswerIndex = Random.Shared.Next(answers.Count);
        }

        var bestAnswer = answers[bestAnswerIndex];
        
        // Wait a bit before accepting (realistic delay)
        await Task.Delay(Random.Shared.Next(3000, 8000), cancellationToken);

        var accepted = await answerGenerator.AcceptAnswerAsync(
            question.Id,
            bestAnswer.Id,
            asker.Token!,
            cancellationToken);

        if (accepted)
        {
            _logger.LogInformation("Accepted answer from user {UserId}", bestAnswer.UserId);
        }

        // Step 7: Add some votes from random users
        if (_options.EnableVoting)
        {
            _logger.LogInformation("Step 7: Adding random votes...");
            
            var voters = allUsers
                .Where(u => u.Profile.Id != asker.Profile.Id && !answerersWithTokens.Any(a => a.userId == u.KeycloakUserId || a.userId == u.Profile.Id))
                .OrderBy(_ => Random.Shared.Next())
                .Take(Random.Shared.Next(2, Math.Min(8, allUsers.Count)))
                .ToList();

            var votersWithTokens = new List<(string userId, string token)>();
            foreach (var voter in voters)
            {
                // Ensure we have a valid token
                if (string.IsNullOrEmpty(voter.Token) && !string.IsNullOrEmpty(voter.KeycloakUserId))
                {
                    voter.Token = await authService.GetUserTokenAsync(voter.KeycloakUserId, cancellationToken);
                }

                if (voter.Token != null)
                {
                    votersWithTokens.Add((voter.KeycloakUserId ?? voter.Profile.Id, voter.Token));
                }
            }

            await votingService.RandomlyVoteOnContentAsync(
                question,
                answers,
                votersWithTokens,
                cancellationToken);
        }

        _logger.LogInformation("=== Seeding operation completed successfully ===");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Data Seeder Service stopping...");
        await base.StopAsync(cancellationToken);
    }
}
