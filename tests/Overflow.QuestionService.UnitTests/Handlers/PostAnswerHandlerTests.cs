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

public class PostAnswerHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<IFusionCache> _cache;
    private readonly PostAnswerHandler _sut;

    public PostAnswerHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        _bus = new Mock<IMessageBus>();
        _cache = new Mock<IFusionCache>();
        var logger = new Mock<ILogger<PostAnswerHandler>>();
        _sut = new PostAnswerHandler(_db, _bus.Object,
            new Ganss.Xss.HtmlSanitizer(), _cache.Object, logger.Object);
    }

    private Question SeedQuestion(string id = "q-1")
    {
        var q = new Question { Id = id, Title = "T", Content = "C", AskerId = "asker-1" };
        _db.Questions.Add(q);
        _db.SaveChanges();
        return q;
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsAnswerAndIncrementsCount()
    {
        // Arrange
        var question = SeedQuestion();
        var command = new PostAnswerCommand(question.Id, "<p>My answer</p>", "answerer-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe("answerer-1");
        result.Value.QuestionId.ShouldBe(question.Id);

        var updated =
            await _db.Questions.FindAsync(new object?[] { question.Id }, TestContext.Current.CancellationToken);
        updated!.AnswerCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishesAnswerCountUpdated()
    {
        // Arrange
        var question = SeedQuestion();
        var command = new PostAnswerCommand(question.Id, "Answer", "user-1");

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _bus.Verify(b => b.PublishAsync(
            It.Is<AnswerCountUpdated>(e => e.QuestionId == question.Id && e.AnswerCount == 1)), Times.Once);
    }

    [Fact]
    public async Task Handle_QuestionNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new PostAnswerCommand("nonexistent", "Answer", "user-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.QuestionNotFound);
    }

    [Fact]
    public async Task Handle_SanitizesAnswerContent()
    {
        // Arrange
        SeedQuestion();
        var command = new PostAnswerCommand("q-1", "<p>OK</p><script>bad</script>", "user-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldNotContain("<script>");
    }
}