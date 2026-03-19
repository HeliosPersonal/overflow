using System.Net;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Models;
using Refit;

namespace Overflow.DataSeederService.Services;

public class VotingService(
    IVoteApiClient voteApi,
    IOptions<SeederOptions> options,
    ILogger<VotingService> logger)
{
    private readonly SeederOptions _options = options.Value;

    public async Task VoteOnQuestionAsync(
        Question question, string voterAuthToken, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVoting)
        {
            return;
        }

        var voteValue = Random.Shared.NextDouble() > 0.3 ? 1 : -1;
        await CastVoteAsync(new CastVoteDto
        {
            TargetId = question.Id,
            TargetType = "Question",
            TargetUserId = question.AskerId,
            QuestionId = question.Id,
            VoteValue = voteValue
        }, voterAuthToken, cancellationToken);
    }

    public async Task VoteOnAnswerAsync(
        Answer answer, string voterAuthToken, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVoting)
        {
            return;
        }

        var voteValue = Random.Shared.NextDouble() > 0.2 ? 1 : -1;
        await CastVoteAsync(new CastVoteDto
        {
            TargetId = answer.Id,
            TargetType = "Answer",
            TargetUserId = answer.UserId,
            QuestionId = answer.QuestionId,
            VoteValue = voteValue
        }, voterAuthToken, cancellationToken);
    }

    private async Task CastVoteAsync(
        CastVoteDto voteDto, string authToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await voteApi.CastVoteAsync(voteDto, $"Bearer {authToken}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Cast {Type} on {TargetType} {TargetId}",
                    voteDto.VoteValue > 0 ? "upvote" : "downvote",
                    voteDto.TargetType, voteDto.TargetId);
            }
            else if (response.StatusCode != HttpStatusCode.BadRequest)
            {
                logger.LogWarning("Failed to cast vote: {StatusCode}", response.StatusCode);
            }
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            // "Already voted" — expected, ignore silently
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error casting vote");
        }
    }

    public async Task RandomlyVoteOnContentAsync(
        Question question, List<Answer> answers,
        List<(string userId, string token)> voters,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVoting || voters.Count == 0)
        {
            return;
        }

        var questionVoters = voters.OrderBy(_ => Random.Shared.Next())
            .Take(Random.Shared.Next(0, Math.Min(voters.Count, 5)));

        foreach (var (_, token) in questionVoters)
        {
            await VoteOnQuestionAsync(question, token, cancellationToken);
            await Task.Delay(Random.Shared.Next(100, 1000), cancellationToken);
        }

        foreach (var answer in answers)
        {
            var answerVoters = voters.OrderBy(_ => Random.Shared.Next())
                .Take(Random.Shared.Next(0, Math.Min(voters.Count, 6)));

            foreach (var (_, token) in answerVoters)
            {
                await VoteOnAnswerAsync(answer, token, cancellationToken);
                await Task.Delay(Random.Shared.Next(100, 1000), cancellationToken);
            }
        }
    }
}