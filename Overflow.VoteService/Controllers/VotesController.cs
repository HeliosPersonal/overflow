using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overflow.Contracts;
using Overflow.Contracts.Helpers;
using Overflow.VoteService.Data;
using Overflow.VoteService.DTOs;
using Overflow.VoteService.Models;
using Wolverine;

namespace Overflow.VoteService.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class VotesController(
    VoteDbContext db,
    IMessageBus bus,
    ILogger<VotesController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CastVote(CastVoteDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            logger.LogWarning("Vote cast attempted without user ID");
            return Unauthorized();
        }

        if (dto.TargetType is not ("Question" or "Answer"))
        {
            logger.LogWarning("Invalid vote target type: {TargetType} for user {UserId}", dto.TargetType, userId);
            return BadRequest("Invalid target type");
        }

        var alreadyVoted = await db.Votes.AnyAsync(x => x.UserId == userId && x.TargetId == dto.TargetId);
        if (alreadyVoted)
        {
            logger.LogDebug("Duplicate vote attempt: {UserId} already voted on {TargetType} {TargetId}",
                userId, dto.TargetType, dto.TargetId);
            return BadRequest("Already voted");
        }

        db.Votes.Add(new Vote
        {
            TargetId = dto.TargetId,
            TargetType = dto.TargetType,
            UserId = userId,
            VoteValue = dto.VoteValue,
            QuestionId = dto.QuestionId
        });

        await db.SaveChangesAsync();

        var reason = (dto.VoteValue, dto.TargetType) switch
        {
            (1, "Question") => ReputationReason.QuestionUpvoted,
            (1, "Answer") => ReputationReason.AnswerUpvoted,
            (-1, "Answer") => ReputationReason.AnswerDownvoted,
            _ => ReputationReason.QuestionDownvoted
        };

        await bus.PublishAsync(ReputationHelper.MakeEvent(dto.TargetUserId, reason, userId));
        await bus.PublishAsync(new VoteCasted(dto.TargetId, dto.TargetType, dto.VoteValue));

        logger.LogInformation("Vote cast: {UserId} voted {VoteValue} on {TargetType} {TargetId}",
            userId, dto.VoteValue, dto.TargetType, dto.TargetId);

        return NoContent();
    }

    [HttpGet("{questionId}")]
    public async Task<IActionResult> GetVotes(string questionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            logger.LogWarning("Get votes attempted without user ID");
            return Unauthorized();
        }

        var votes = await db.Votes
            .Where(x => x.UserId == userId && x.QuestionId == questionId)
            .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
            .ToListAsync();

        logger.LogDebug("Retrieved {Count} votes for user {UserId} on question {QuestionId}",
            votes.Count, userId, questionId);

        return Ok(votes);
    }
}