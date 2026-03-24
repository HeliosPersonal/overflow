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

public class DeleteQuestionHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<IFusionCache> _cache;
    private readonly DeleteQuestionHandler _sut;

    public DeleteQuestionHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        _bus = new Mock<IMessageBus>();
        _cache = new Mock<IFusionCache>();
        var logger = new Mock<ILogger<DeleteQuestionHandler>>();
        _sut = new DeleteQuestionHandler(_db, _bus.Object, _cache.Object, logger.Object);
    }

    [Fact]
    public async Task Handle_OwnerDeletes_RemovesQuestionAndPublishesEvent()
    {
        // Arrange
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "owner-1" };
        _db.Questions.Add(q);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new DeleteQuestionCommand("q-1", "owner-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _db.Questions.ShouldBeEmpty();

        _bus.Verify(b => b.PublishAsync(
            It.Is<QuestionDeleted>(e => e.QuestionId == "q-1")), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOwnerDeletes_ReturnsForbidden()
    {
        // Arrange
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "owner-1" };
        _db.Questions.Add(q);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new DeleteQuestionCommand("q-1", "other-user");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.Forbidden);
        _db.Questions.Count().ShouldBe(1);
    }

    [Fact]
    public async Task Handle_QuestionNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new DeleteQuestionCommand("nonexistent", "user-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.QuestionNotFound);
    }
}