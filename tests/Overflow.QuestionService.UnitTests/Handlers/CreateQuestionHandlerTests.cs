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

public class CreateQuestionHandlerTests
{
    private readonly QuestionDbContext _db;

    public CreateQuestionHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();

        _db.Tags.AddRange(
            new Tag { Name = "C#", Slug = "csharp", Description = "C# language" },
            new Tag { Name = "ASP.NET", Slug = "aspnet", Description = "ASP.NET framework" }
        );
        _db.SaveChanges();
    }

    private static CreateQuestionHandler CreateHandler(
        QuestionDbContext db, Mock<TagService>? tagService = null)
    {
        var bus = new Mock<IMessageBus>();
        var sanitizer = new HtmlSanitizer();
        var cache = new Mock<IFusionCache>();
        var logger = new Mock<ILogger<CreateQuestionHandler>>();
        var ts = tagService ?? CreateTagServiceMock(true);
        return new(db, bus.Object, ts.Object, sanitizer, cache.Object, logger.Object);
    }

    private static Mock<TagService> CreateTagServiceMock(bool valid)
    {
        var mock = new Mock<TagService>(
            Mock.Of<IFusionCache>(),
            null! /* QuestionDbContext — won't be used because we mock the method */);
        mock.Setup(x => x.AreTagsValidAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(valid);
        return mock;
    }

    [Fact]
    public async Task Handle_InvalidTags_ReturnsFailure()
    {
        // Arrange
        var invalidTagService = CreateTagServiceMock(false);
        var handler = CreateHandler(_db, invalidTagService);
        var command = new CreateQuestionCommand("Title", "Body", ["nonexistent"], "user-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.InvalidTags);
        _db.Questions.ShouldBeEmpty();
    }
}