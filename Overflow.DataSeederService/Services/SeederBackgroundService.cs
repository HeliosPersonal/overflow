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
        _logger.LogInformation("════════════════════════════════════════════════════════════════");
        _logger.LogInformation("🌱 STARTING DATA SEEDING CYCLE");
        _logger.LogInformation("════════════════════════════════════════════════════════════════");

        using var scope = _serviceProvider.CreateScope();
        
        var userGenerator = scope.ServiceProvider.GetRequiredService<UserGenerator>();
        var questionGenerator = scope.ServiceProvider.GetRequiredService<QuestionGenerator>();
        var answerGenerator = scope.ServiceProvider.GetRequiredService<AnswerGenerator>();
        var votingService = scope.ServiceProvider.GetRequiredService<VotingService>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
        var llmClient = scope.ServiceProvider.GetRequiredService<LlmClient>();

        try
        {
            // ═══════════════════════════════════════════════════════════════
            // STEP 1: User Pool Management
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("");
            _logger.LogInformation("📋 STEP 1: Managing Seeder User Pool");
            _logger.LogInformation("─────────────────────────────────────────────────");
            
            var userPool = await userGenerator.GetOrCreateUserPoolAsync(cancellationToken);
            
            if (userPool.Count < 3)
            {
                _logger.LogError("❌ STEP 1 FAILED: Not enough users ({Count}). Need at least 3 users.", userPool.Count);
                return;
            }

            _logger.LogInformation("✅ STEP 1 COMPLETE: User pool ready with {Count} users", userPool.Count);
            
            var allUsers = userPool;

            // ═══════════════════════════════════════════════════════════════
            // STEP 2: Select Question Asker
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("");
            _logger.LogInformation("👤 STEP 2: Selecting Question Asker");
            _logger.LogInformation("─────────────────────────────────────────────────");
            
            var asker = allUsers[Random.Shared.Next(allUsers.Count)];
            _logger.LogInformation("Selected: {DisplayName} (Keycloak ID: {KeycloakId})", 
                asker.Profile.DisplayName, asker.KeycloakUserId);

            // Ensure we have a valid token (refresh if needed)
            if (string.IsNullOrEmpty(asker.Token) && !string.IsNullOrEmpty(asker.KeycloakUserId))
            {
                _logger.LogDebug("Refreshing token for asker...");
                asker.Token = await authService.GetUserTokenAsync(asker.KeycloakUserId, cancellationToken);
            }

            if (asker.Token == null)
            {
                _logger.LogError("❌ STEP 2 FAILED: Could not get authentication token for asker");
                return;
            }
            
            _logger.LogInformation("✅ STEP 2 COMPLETE: Asker ready with valid token");

            // ═══════════════════════════════════════════════════════════════
            // STEP 3: Generate Question
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("");
            _logger.LogInformation("❓ STEP 3: Generating Question");
            _logger.LogInformation("─────────────────────────────────────────────────");
            
            var question = await questionGenerator.CreateQuestionAsync(
                asker.KeycloakUserId ?? asker.Profile.Id,
                asker.Token,
                cancellationToken);

            if (question == null)
            {
                _logger.LogError("❌ STEP 3 FAILED: Could not create question");
                return;
            }

            _logger.LogInformation("✅ STEP 3 COMPLETE: Question created");
            _logger.LogInformation("   📝 Title: {Title}", question.Title);
            _logger.LogInformation("   🆔 ID: {Id}", question.Id);

            // ═══════════════════════════════════════════════════════════════
            // STEP 4: Realistic Delay
            // ═══════════════════════════════════════════════════════════════
            var delaySeconds = Random.Shared.Next(5, 15);
            _logger.LogInformation("");
            _logger.LogInformation("⏳ STEP 4: Waiting {Seconds}s (realistic delay before answers)", delaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            _logger.LogInformation("✅ STEP 4 COMPLETE");

            // ═══════════════════════════════════════════════════════════════
            // STEP 5: Generate Answers
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("");
            _logger.LogInformation("💬 STEP 5: Generating Answers");
            _logger.LogInformation("─────────────────────────────────────────────────");
            
            var potentialAnswerers = allUsers.Where(u => u.Profile.Id != asker.Profile.Id).ToList();
            if (potentialAnswerers.Count == 0)
            {
                _logger.LogError("❌ STEP 5 FAILED: No users available to answer (excluding asker)");
                return;
            }

            var answerCount = Random.Shared.Next(_options.MinAnswersPerQuestion, _options.MaxAnswersPerQuestion + 1);
            answerCount = Math.Min(answerCount, potentialAnswerers.Count);

            var answerers = potentialAnswerers
                .OrderBy(_ => Random.Shared.Next())
                .Take(answerCount)
                .ToList();

            _logger.LogInformation("Selected {Count} answerers:", answerCount);

            var answerersWithTokens = new List<(string userId, string token)>();
            foreach (var answerer in answerers)
            {
                // Ensure we have a valid token - always refresh to avoid expiration
                if (!string.IsNullOrEmpty(answerer.KeycloakUserId))
                {
                    _logger.LogDebug("Refreshing token for user {DisplayName} (Keycloak ID: {KeycloakId})", 
                        answerer.Profile.DisplayName, answerer.KeycloakUserId);
                    answerer.Token = await authService.GetUserTokenAsync(answerer.KeycloakUserId, cancellationToken);
                }

                if (answerer.Token != null)
                {
                    answerersWithTokens.Add((answerer.KeycloakUserId ?? answerer.Profile.Id, answerer.Token));
                    _logger.LogInformation("   👤 {DisplayName} (ready)", answerer.Profile.DisplayName);
                }
                else
                {
                    _logger.LogWarning("   ⚠️  {DisplayName} (no valid token, skipping)", answerer.Profile.DisplayName);
                }
            }

            if (answerersWithTokens.Count == 0)
            {
                _logger.LogError("❌ STEP 5 FAILED: No answerers have valid tokens");
                return;
            }

            var answers = await answerGenerator.CreateMultipleAnswersAsync(
                question,
                answerersWithTokens,
                cancellationToken);

            if (answers.Count == 0)
            {
                _logger.LogError("❌ STEP 5 FAILED: No answers were created");
                return;
            }

            _logger.LogInformation("✅ STEP 5 COMPLETE: Created {Count} answer(s)", answers.Count);

            // ═══════════════════════════════════════════════════════════════
            // STEP 6: Select Best Answer
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("");
            _logger.LogInformation("🏆 STEP 6: Selecting Best Answer");
            _logger.LogInformation("─────────────────────────────────────────────────");
            
            int bestAnswerIndex;
            if (_options.EnableLlmGeneration && answers.Count > 1)
            {
                _logger.LogInformation("Using LLM to select best answer...");
                var answerContents = answers.Select(a => a.Content).ToList();
                bestAnswerIndex = await llmClient.SelectBestAnswerAsync(
                    question.Title,
                    answerContents,
                    cancellationToken);
            }
            else
            {
                bestAnswerIndex = Random.Shared.Next(answers.Count);
                _logger.LogInformation("Randomly selected answer {Index}", bestAnswerIndex);
            }

            var bestAnswer = answers[bestAnswerIndex];
            
            // Wait a bit before accepting (realistic delay)
            var acceptDelay = Random.Shared.Next(3, 8);
            _logger.LogInformation("Waiting {Seconds}s before accepting answer...", acceptDelay);
            await Task.Delay(TimeSpan.FromSeconds(acceptDelay), cancellationToken);

            _logger.LogInformation("✅ STEP 6 COMPLETE: Best answer selected (Index: {Index})", bestAnswerIndex);

            // ═══════════════════════════════════════════════════════════════
            // STEP 7: Accept Answer
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("");
            _logger.LogInformation("✅ STEP 7: Accepting Best Answer");
            _logger.LogInformation("─────────────────────────────────────────────────");

            // Refresh the asker token — it may have expired during the LLM pipeline
            if (!string.IsNullOrEmpty(asker.KeycloakUserId))
            {
                _logger.LogDebug("Refreshing asker token before accept...");
                asker.Token = await authService.GetUserTokenAsync(asker.KeycloakUserId, cancellationToken);
            }

            if (string.IsNullOrEmpty(asker.Token))
            {
                _logger.LogWarning("⚠️  STEP 7 WARNING: Could not refresh asker token, skipping accept");
            }
            else
            {
                var accepted = await answerGenerator.AcceptAnswerAsync(
                    question.Id,
                    bestAnswer.Id,
                    asker.Token!,
                    cancellationToken);

                if (accepted)
                {
                    _logger.LogInformation("✅ STEP 7 COMPLETE: Answer accepted by {DisplayName}", asker.Profile.DisplayName);
                }
                else
                {
                    _logger.LogWarning("⚠️  STEP 7 WARNING: Failed to accept answer (not critical)");
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // STEP 8: Add Random Votes
            // ═══════════════════════════════════════════════════════════════
            if (_options.EnableVoting)
            {
                _logger.LogInformation("");
                _logger.LogInformation("🗳️  STEP 8: Adding Random Votes");
                _logger.LogInformation("─────────────────────────────────────────────────");
                
                var voters = allUsers
                    .Where(u => u.Profile.Id != asker.Profile.Id && !answerersWithTokens.Any(a => a.userId == u.KeycloakUserId || a.userId == u.Profile.Id))
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(Random.Shared.Next(2, Math.Min(8, allUsers.Count)))
                    .ToList();

                _logger.LogInformation("Selected {Count} voters", voters.Count);

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
                
                _logger.LogInformation("✅ STEP 8 COMPLETE: Votes added");
            }

            _logger.LogInformation("");
            _logger.LogInformation("════════════════════════════════════════════════════════════════");
            _logger.LogInformation("🎉 SEEDING CYCLE COMPLETED SUCCESSFULLY");
            _logger.LogInformation("════════════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger.LogError("");
            _logger.LogError("════════════════════════════════════════════════════════════════");
            _logger.LogError(ex, "💥 SEEDING CYCLE FAILED");
            _logger.LogError("════════════════════════════════════════════════════════════════");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Data Seeder Service stopping...");
        await base.StopAsync(cancellationToken);
    }
}
