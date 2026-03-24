using Ganss.Xss;
using Microsoft.Extensions.Logging;
using Moq;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Features.Questions.Commands;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.UnitTests.Helpers;
using Shouldly;

namespace Overflow.QuestionService.UnitTests.Handlers;

public class UpdateAnswerHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly UpdateAnswerHandler _sut;

    public UpdateAnswerHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        var logger = new Mock<ILogger<UpdateAnswerHandler>>();
        _sut = new UpdateAnswerHandler(_db, new HtmlSanitizer(), logger.Object);
    }

    [Fact]
    public async Task Handle_ValidUpdate_UpdatesContentAndTimestamp()
    {
        // Arrange
        var a = new Answer { Id = "a-1", Content = "Old", UserId = "u-1", QuestionId = "q-1" };
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new UpdateAnswerCommand("q-1", "a-1", "<p>New content</p>");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updated = await _db.Answers.FindAsync(new object?[] { "a-1" }, TestContext.Current.CancellationToken);
        updated!.Content.ShouldBe("<p>New content</p>");
        updated.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_AnswerNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new UpdateAnswerCommand("q-1", "nonexistent", "New");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.AnswerNotFound);
    }

    [Fact]
    public async Task Handle_AnswerBelongsToDifferentQuestion_ReturnsFailure()
    {
        // Arrange
        var a = new Answer { Id = "a-1", Content = "Old", UserId = "u-1", QuestionId = "q-2" };
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new UpdateAnswerCommand("q-1", "a-1", "New");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.AnswerNotFound);
    }

    [Fact]
    public async Task Handle_SanitizesHtml()
    {
        // Arrange
        var a = new Answer { Id = "a-1", Content = "Old", UserId = "u-1", QuestionId = "q-1" };
        _db.Answers.Add(a);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new UpdateAnswerCommand("q-1", "a-1", "<p>OK</p><script>xss</script>");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updated = await _db.Answers.FindAsync(new object?[] { "a-1" }, TestContext.Current.CancellationToken);
        updated!.Content.ShouldNotContain("<script>");
    }
}