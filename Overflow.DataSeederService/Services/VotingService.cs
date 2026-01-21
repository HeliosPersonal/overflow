using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using System.Net.Http.Json;

namespace Overflow.DataSeederService.Services;

public class VotingService
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly ILogger<VotingService> _logger;

    public VotingService(
        HttpClient httpClient,
        IOptions<SeederOptions> options,
        ILogger<VotingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task VoteOnQuestionAsync(
        Question question,
        string voterAuthToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVoting)
            return;

        // 70% upvote, 30% downvote - bias towards positive
        var voteValue = Random.Shared.NextDouble() > 0.3 ? 1 : -1;

        var voteDto = new CastVoteDto
        {
            TargetId = question.Id,
            TargetType = "Question",
            TargetUserId = question.AskerId,
            QuestionId = question.Id,
            VoteValue = voteValue
        };

        await CastVoteAsync(voteDto, voterAuthToken, cancellationToken);
    }

    public async Task VoteOnAnswerAsync(
        Answer answer,
        string voterAuthToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVoting)
            return;

        // 80% upvote, 20% downvote - bias towards helpful answers
        var voteValue = Random.Shared.NextDouble() > 0.2 ? 1 : -1;

        var voteDto = new CastVoteDto
        {
            TargetId = answer.Id,
            TargetType = "Answer",
            TargetUserId = answer.UserId,
            QuestionId = answer.QuestionId,
            VoteValue = voteValue
        };

        await CastVoteAsync(voteDto, voterAuthToken, cancellationToken);
    }

    private async Task CastVoteAsync(
        CastVoteDto voteDto,
        string authToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.VoteServiceUrl}/votes");
            request.Headers.Add("Authorization", $"Bearer {authToken}");
            request.Content = JsonContent.Create(voteDto);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Cast {VoteType} vote on {TargetType} {TargetId}",
                    voteDto.VoteValue > 0 ? "upvote" : "downvote",
                    voteDto.TargetType,
                    voteDto.TargetId);
            }
            else
            {
                // Don't log warnings for "already voted" errors as they're expected
                if (response.StatusCode != System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("Failed to cast vote: {StatusCode}", response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote");
        }
    }

    public async Task RandomlyVoteOnContentAsync(
        Question question,
        List<Answer> answers,
        List<(string userId, string token)> voters,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVoting || voters.Count == 0)
            return;

        // Randomly select some users to vote on the question
        var questionVoterCount = Random.Shared.Next(0, Math.Min(voters.Count, 5));
        var questionVoters = voters.OrderBy(_ => Random.Shared.Next())
            .Take(questionVoterCount);

        foreach (var (_, token) in questionVoters)
        {
            await VoteOnQuestionAsync(question, token, cancellationToken);
            await Task.Delay(Random.Shared.Next(100, 1000), cancellationToken);
        }

        // Vote on answers
        foreach (var answer in answers)
        {
            var answerVoterCount = Random.Shared.Next(0, Math.Min(voters.Count, 6));
            var answerVoters = voters.OrderBy(_ => Random.Shared.Next())
                .Take(answerVoterCount);

            foreach (var (_, token) in answerVoters)
            {
                await VoteOnAnswerAsync(answer, token, cancellationToken);
                await Task.Delay(Random.Shared.Next(100, 1000), cancellationToken);
            }
        }
    }
}
