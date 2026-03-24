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

public class AcceptAnswerHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<IFusionCache> _cache;
    private readonly AcceptAnswerHandler _sut;

    public AcceptAnswerHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        _bus = new Mock<IMessageBus>();
        _cache = new Mock<IFusionCache>();
        var logger = new Mock<ILogger<AcceptAnswerHandler>>();
        _sut = new AcceptAnswerHandler(_db, _bus.Object, _cache.Object, logger.Object);
    }

    private (Question question, Answer answer) SeedQuestionWithAnswer()
    {
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "asker-1" };
        var a = new Answer { Id = "a-1", Content = "A", UserId = "answerer-1", QuestionId = "q-1" };
        _db.Questions.Add(q);
        _db.Answers.Add(a);
        _db.SaveChanges();
        return (q, a);
    }

    [Fact]
    public async Task Handle_ValidAccept_MarksAnswerAcceptedAndPublishesEvents()
    {
        // Arrange
        var (question, answer) = SeedQuestionWithAnswer();
        var command = new AcceptAnswerCommand(question.Id, answer.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedAnswer =
            await _db.Answers.FindAsync(new object?[] { answer.Id }, TestContext.Current.CancellationToken);
        updatedAnswer!.Accepted.ShouldBeTrue();

        var updatedQuestion =
            await _db.Questions.FindAsync(new object?[] { question.Id }, TestContext.Current.CancellationToken);
        updatedQuestion!.HasAcceptedAnswer.ShouldBeTrue();

        _bus.Verify(b => b.PublishAsync(
            It.Is<AnswerAccepted>(e => e.QuestionId == "q-1")), Times.Once);

        _bus.Verify(b => b.PublishAsync(
            It.Is<UserReputationChanged>(e => e.UserId == "answerer-1" && e.Delta == 15)), Times.Once);
    }

    [Fact]
    public async Task Handle_QuestionAlreadyHasAcceptedAnswer_ReturnsFailure()
    {
        // Arrange
        var (question, answer) = SeedQuestionWithAnswer();
        question.HasAcceptedAnswer = true;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new AcceptAnswerCommand(question.Id, answer.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.Forbidden);
    }

    [Fact]
    public async Task Handle_AnswerBelongsToDifferentQuestion_ReturnsFailure()
    {
        // Arrange
        var q1 = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "asker-1" };
        var q2 = new Question { Id = "q-2", Title = "T2", Content = "C2", AskerId = "asker-2" };
        var a = new Answer { Id = "a-1", Content = "A", UserId = "u-1", QuestionId = "q-2" };
        _db.Questions.AddRange(q1, q2);
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new AcceptAnswerCommand("q-1", "a-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.Forbidden);
    }

    [Fact]
    public async Task Handle_NonExistentAnswer_ReturnsNotFound()
    {
        // Arrange
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "asker-1" };
        _db.Questions.Add(q);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new AcceptAnswerCommand("q-1", "nonexistent");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.NotFound);
    }
}