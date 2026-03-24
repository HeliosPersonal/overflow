using Moq;
using Overflow.Contracts;
using Overflow.VoteService.Data;
using Overflow.VoteService.Features.Votes.Commands;
using Overflow.VoteService.Models;
using Overflow.VoteService.UnitTests.Helpers;
using Shouldly;
using Wolverine;

namespace Overflow.VoteService.UnitTests.Handlers;

public class CastVoteHandlerTests
{
    private readonly VoteDbContext _db;
    private readonly Mock<IMessageBus> _bus;
    private readonly CastVoteHandler _sut;

    public CastVoteHandlerTests()
    {
        _db = DbContextFactory.CreateVoteDb();
        _bus = new Mock<IMessageBus>();
        _sut = new CastVoteHandler(_db, _bus.Object);
    }

    [Fact]
    public async Task Handle_ValidUpvoteOnQuestion_SavesVoteAndPublishesEvents()
    {
        // Arrange
        var command = new CastVoteCommand("voter-1", "q-1", VoteTargetType.Question, "author-1", "q-1", 1);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _db.Votes.Count().ShouldBe(1);

        var vote = _db.Votes.First();
        vote.UserId.ShouldBe("voter-1");
        vote.TargetId.ShouldBe("q-1");
        vote.VoteValue.ShouldBe(1);

        // Publishes reputation event (QuestionUpvoted → +5)
        _bus.Verify(b => b.PublishAsync(
            It.Is<UserReputationChanged>(e => e.UserId == "author-1" && e.Delta == 5)), Times.Once);

        // Publishes VoteCasted
        _bus.Verify(b => b.PublishAsync(
            It.Is<VoteCasted>(e => e.TargetId == "q-1" && e.VoteValue == 1)), Times.Once);
    }

    [Fact]
    public async Task Handle_DownvoteOnAnswer_CalculatesCorrectDelta()
    {
        // Arrange
        var command = new CastVoteCommand("voter-1", "a-1", VoteTargetType.Answer, "author-1", "q-1", -1);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _bus.Verify(b => b.PublishAsync(
                It.Is<UserReputationChanged>(e => e.Delta == -2 && e.Reason == ReputationReason.AnswerDownvoted)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UpvoteOnAnswer_CalculatesCorrectDelta()
    {
        // Arrange
        var command = new CastVoteCommand("voter-1", "a-1", VoteTargetType.Answer, "author-1", "q-1", 1);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _bus.Verify(b => b.PublishAsync(
            It.Is<UserReputationChanged>(e => e.Delta == 5 && e.Reason == ReputationReason.AnswerUpvoted)), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidTargetType_ReturnsFailure()
    {
        // Arrange
        var command = new CastVoteCommand("voter-1", "x-1", "InvalidType", "author-1", "q-1", 1);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Invalid target type");
        _db.Votes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_AlreadyVoted_ReturnsFailure()
    {
        // Arrange
        _db.Votes.Add(new Vote
        {
            UserId = "voter-1", TargetId = "q-1", TargetType = VoteTargetType.Question,
            QuestionId = "q-1", VoteValue = 1
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new CastVoteCommand("voter-1", "q-1", VoteTargetType.Question, "author-1", "q-1", 1);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Already voted");
    }

    [Fact]
    public async Task Handle_DifferentUsersCanVoteOnSameTarget()
    {
        // Arrange
        _db.Votes.Add(new Vote
        {
            UserId = "voter-1", TargetId = "q-1", TargetType = VoteTargetType.Question,
            QuestionId = "q-1", VoteValue = 1
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new CastVoteCommand("voter-2", "q-1", VoteTargetType.Question, "author-1", "q-1", 1);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _db.Votes.Count().ShouldBe(2);
    }
}