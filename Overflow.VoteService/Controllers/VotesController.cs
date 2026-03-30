using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Common.CommonExtensions;
using Overflow.VoteService.DTOs;
using Overflow.VoteService.Features.Votes.Commands;
using Overflow.VoteService.Features.Votes.Queries;

namespace Overflow.VoteService.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class VotesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CastVote(CastVoteDto dto)
    {
        var userId = User.GetRequiredUserId();
        var result = await sender.Send(new CastVoteCommand(
            userId, dto.TargetId, dto.TargetType, dto.TargetUserId, dto.QuestionId, dto.VoteValue));

        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpGet("{questionId}")]
    public async Task<IActionResult> GetVotes(string questionId)
    {
        var userId = User.GetRequiredUserId();
        var votes = await sender.Send(new GetUserVotesQuery(userId, questionId));
        return Ok(votes);
    }
}