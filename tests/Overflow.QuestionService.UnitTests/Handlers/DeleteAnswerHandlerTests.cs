using Microsoft.Extensions.Logging;
using Moq;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Features.Questions.Commands;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.UnitTests.Helpers;
using Shouldly;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.UnitTests.Handlers;

public class DeleteAnswerHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<IFusionCache> _cache;
    private readonly DeleteAnswerHandler _sut;

    public DeleteAnswerHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        _bus = new Mock<IMessageBus>();
        _cache = new Mock<IFusionCache>();
        var logger = new Mock<ILogger<DeleteAnswerHandler>>();
        _sut = new DeleteAnswerHandler(_db, _bus.Object, _cache.Object, logger.Object);
    }

    [Fact]
    public async Task Handle_ValidDelete_RemovesAnswerAndDecrementsCount()
    {
        // Arrange
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "u-1", AnswerCount = 1 };
        var a = new Answer { Id = "a-1", Content = "A", UserId = "u-2", QuestionId = "q-1" };
        _db.Questions.Add(q);
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new DeleteAnswerCommand("q-1", "a-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _db.Answers.ShouldBeEmpty();

        var updated = await _db.Questions.FindAsync(new object?[] { "q-1" }, TestContext.Current.CancellationToken);
        updated!.AnswerCount.ShouldBe(0);

        _bus.Verify(b => b.PublishAsync(
            It.Is<AnswerCountUpdated>(e => e.QuestionId == "q-1" && e.AnswerCount == 0)), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptedAnswer_ReturnsForbidden()
    {
        // Arrange
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "u-1", AnswerCount = 1 };
        var a = new Answer { Id = "a-1", Content = "A", UserId = "u-2", QuestionId = "q-1", Accepted = true };
        _db.Questions.Add(q);
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new DeleteAnswerCommand("q-1", "a-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.Forbidden);
        _db.Answers.Count().ShouldBe(1);
    }

    [Fact]
    public async Task Handle_AnswerBelongsToDifferentQuestion_ReturnsForbidden()
    {
        // Arrange
        var q1 = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "u-1" };
        var q2 = new Question { Id = "q-2", Title = "T", Content = "C", AskerId = "u-1" };
        var a = new Answer { Id = "a-1", Content = "A", UserId = "u-2", QuestionId = "q-2" };
        _db.Questions.AddRange(q1, q2);
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new DeleteAnswerCommand("q-1", "a-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.Forbidden);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsFailure()
    {
        // Arrange
        var command = new DeleteAnswerCommand("q-1", "a-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.NotFound);
    }
}