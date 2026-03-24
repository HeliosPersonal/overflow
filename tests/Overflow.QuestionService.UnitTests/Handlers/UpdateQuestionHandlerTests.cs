using Ganss.Xss;
using Microsoft.Extensions.Logging;
using Moq;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Features.Questions.Commands;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.Services;
using Overflow.QuestionService.UnitTests.Helpers;
using Shouldly;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.UnitTests.Handlers;

public class UpdateQuestionHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly UpdateQuestionHandler _sut;

    public UpdateQuestionHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        var bus = new Mock<IMessageBus>();
        var cache = new Mock<IFusionCache>();
        var logger = new Mock<ILogger<UpdateQuestionHandler>>();

        var tagService = new Mock<TagService>(Mock.Of<IFusionCache>(), null!);
        tagService.Setup(x => x.AreTagsValidAsync(It.IsAny<List<string>>())).ReturnsAsync(true);

        _sut = new UpdateQuestionHandler(_db, bus.Object, tagService.Object,
            new HtmlSanitizer(), cache.Object, logger.Object);
    }

    [Fact]
    public async Task Handle_NonOwner_ReturnsForbidden()
    {
        // Arrange
        var q = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "owner-1" };
        _db.Questions.Add(q);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new UpdateQuestionCommand("q-1", "New", "New", ["csharp"], "other-user");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.Forbidden);
    }

    [Fact]
    public async Task Handle_QuestionNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new UpdateQuestionCommand("nonexistent", "T", "C", ["csharp"], "user-1");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.QuestionNotFound);
    }
}