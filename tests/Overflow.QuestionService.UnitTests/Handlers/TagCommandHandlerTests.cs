using Moq;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Features.Tags.Commands;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.Services;
using Overflow.QuestionService.UnitTests.Helpers;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.UnitTests.Handlers;

public class TagCommandHandlerTests
{
    private readonly QuestionDbContext _db;
    private readonly Mock<IFusionCache> _cache;
    private readonly Mock<TagService> _tagService;

    public TagCommandHandlerTests()
    {
        _db = DbContextFactory.CreateQuestionDb();
        _cache = new Mock<IFusionCache>();
        _tagService = new Mock<TagService>(Mock.Of<IFusionCache>(), null!);
    }

    [Fact]
    public async Task CreateTag_ValidSlug_CreatesAndInvalidatesCache()
    {
        // Arrange
        var handler = new CreateTagHandler(_db, _tagService.Object, _cache.Object);
        var command = new CreateTagCommand("react", "React", "A JS library");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Slug.ShouldBe("react");
        result.Value.Name.ShouldBe("React");
        _db.Tags.Count().ShouldBe(1);
        _tagService.Verify(s => s.InvalidateCache(), Times.Once);
    }

    [Fact]
    public async Task CreateTag_DuplicateSlug_ReturnsFailure()
    {
        // Arrange
        _db.Tags.Add(new Tag { Name = "React", Slug = "react", Description = "Existing" });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new CreateTagHandler(_db, _tagService.Object, _cache.Object);
        var command = new CreateTagCommand("react", "React Duplicate", "Dup");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("already exists");
    }

    [Fact]
    public async Task UpdateTag_ExistingTag_UpdatesFields()
    {
        // Arrange
        _db.Tags.Add(new Tag { Id = "react", Name = "React", Slug = "react", Description = "Old desc" });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new UpdateTagHandler(_db, _tagService.Object, _cache.Object);
        var command = new UpdateTagCommand("react", "React.js", "New description");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("React.js");
        result.Value.Description.ShouldBe("New description");
    }

    [Fact]
    public async Task UpdateTag_NotFound_ReturnsFailure()
    {
        // Arrange
        var handler = new UpdateTagHandler(_db, _tagService.Object, _cache.Object);
        var command = new UpdateTagCommand("nonexistent", "N", "D");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.NotFound);
    }

    [Fact]
    public async Task DeleteTag_ExistingTag_RemovesAndInvalidatesCache()
    {
        // Arrange
        _db.Tags.Add(new Tag { Id = "react", Name = "React", Slug = "react", Description = "Desc" });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new DeleteTagHandler(_db, _tagService.Object, _cache.Object);
        var command = new DeleteTagCommand("react");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _db.Tags.ShouldBeEmpty();
        _tagService.Verify(s => s.InvalidateCache(), Times.Once);
    }

    [Fact]
    public async Task DeleteTag_NotFound_ReturnsFailure()
    {
        // Arrange
        var handler = new DeleteTagHandler(_db, _tagService.Object, _cache.Object);
        var command = new DeleteTagCommand("nonexistent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainErrors.NotFound);
    }
}