using System.Security.Claims;
using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Overflow.VoteService.Controllers;
using Overflow.VoteService.DTOs;
using Overflow.VoteService.Features.Votes.Commands;
using Overflow.VoteService.Features.Votes.Queries;
using Shouldly;

namespace Overflow.VoteService.UnitTests.Controllers;

public class VotesControllerTests
{
    private readonly Mock<ISender> _sender;
    private readonly VotesController _sut;

    public VotesControllerTests()
    {
        _sender = new Mock<ISender>();
        _sut = new VotesController(_sender.Object);
    }

    private void SetUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task CastVote_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        SetUser("voter-1");
        _sender.Setup(s => s.Send(It.IsAny<CastVoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var dto = new CastVoteDto("q-1", "Question", "author-1", "q-1", 1);

        // Act
        var result = await _sut.CastVote(dto);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CastVote_HandlerFailure_ReturnsBadRequest()
    {
        // Arrange
        SetUser("voter-1");
        _sender.Setup(s => s.Send(It.IsAny<CastVoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Already voted"));

        var dto = new CastVoteDto("q-1", "Question", "author-1", "q-1", 1);

        // Act
        var result = await _sut.CastVote(dto);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CastVote_NoUser_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var dto = new CastVoteDto("q-1", "Question", "author-1", "q-1", 1);

        // Act
        var result = await _sut.CastVote(dto);

        // Assert
        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetVotes_ReturnsOkWithResults()
    {
        // Arrange
        SetUser("user-1");
        var votes = new List<UserVotesResult> { new("q-1", "Question", 1) };
        _sender.Setup(s => s.Send(It.IsAny<GetUserVotesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(votes);

        // Act
        var result = await _sut.GetVotes("q-1");

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        ((List<UserVotesResult>)ok.Value!).Count.ShouldBe(1);
    }
}