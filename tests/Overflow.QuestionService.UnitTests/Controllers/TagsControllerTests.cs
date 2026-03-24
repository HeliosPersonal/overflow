using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Overflow.QuestionService.Controllers;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Features.Tags.Commands;
using Overflow.QuestionService.Features.Tags.Queries;
using Overflow.QuestionService.Models;
using Shouldly;

namespace Overflow.QuestionService.UnitTests.Controllers;

public class TagsControllerTests
{
    private readonly Mock<ISender> _sender;
    private readonly TagsController _sut;

    public TagsControllerTests()
    {
        _sender = new Mock<ISender>();
        _sut = new TagsController(_sender.Object);
    }

    [Fact]
    public async Task GetTags_ReturnsOkWithTagList()
    {
        // Arrange
        var tags = new List<Tag> { new() { Name = "C#", Slug = "csharp", Description = "D" } };
        _sender.Setup(s => s.Send(It.IsAny<GetTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        // Act
        var result = await _sut.GetTags("name");

        // Assert
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ((List<Tag>)ok.Value!).Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTag_Success_ReturnsCreatedAtAction()
    {
        // Arrange
        var tag = new Tag { Id = "react", Name = "React", Slug = "react", Description = "D" };
        _sender.Setup(s => s.Send(It.IsAny<CreateTagCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(tag));

        // Act
        var result = await _sut.CreateTag(new CreateTagDto("React", "react", "D"));

        // Assert
        result.Result.ShouldBeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateTag_Duplicate_ReturnsConflict()
    {
        // Arrange
        _sender.Setup(s => s.Send(It.IsAny<CreateTagCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Tag>("A tag with slug 'react' already exists."));

        // Act
        var result = await _sut.CreateTag(new CreateTagDto("React", "react", "D"));

        // Assert
        result.Result.ShouldBeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task GetTag_NotFound_Returns404()
    {
        // Arrange
        _sender.Setup(s => s.Send(It.IsAny<GetTagByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tag?)null);

        // Act
        var result = await _sut.GetTag("nonexistent");

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteTag_Success_ReturnsNoContent()
    {
        // Arrange
        _sender.Setup(s => s.Send(It.IsAny<DeleteTagCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.DeleteTag("react");

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }
}