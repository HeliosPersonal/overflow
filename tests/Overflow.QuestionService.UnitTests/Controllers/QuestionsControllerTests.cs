using System.Security.Claims;
using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Overflow.Common;
using Overflow.QuestionService.Controllers;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Features.Questions.Commands;
using Overflow.QuestionService.Features.Questions.Queries;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.RequestHelpers;
using Shouldly;

namespace Overflow.QuestionService.UnitTests.Controllers;

public class QuestionsControllerTests
{
    private readonly Mock<ISender> _sender;
    private readonly QuestionsController _sut;

    public QuestionsControllerTests()
    {
        _sender = new Mock<ISender>();
        _sut = new QuestionsController(_sender.Object);
    }

    private void SetUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private void SetAnonymousUser()
    {
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // ── CreateQuestion ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateQuestion_ValidRequest_ReturnsCreated()
    {
        // Arrange
        SetUser("user-1");
        var dto = new CreateQuestionDto("Title", "Content", ["csharp"]);
        var question = new Question { Id = "q-1", Title = "Title", Content = "Content", AskerId = "user-1" };
        _sender.Setup(s => s.Send(It.IsAny<CreateQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(question));

        // Act
        var result = await _sut.CreateQuestion(dto);

        // Assert
        var created = result.Result.ShouldBeOfType<CreatedResult>();
        created.Location.ShouldContain("q-1");
    }

    [Fact]
    public async Task CreateQuestion_NoUser_ReturnsBadRequest()
    {
        // Arrange
        SetAnonymousUser();

        // Act
        var result = await _sut.CreateQuestion(new CreateQuestionDto("T", "C", ["csharp"]));

        // Assert
        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateQuestion_HandlerFailure_ReturnsBadRequest()
    {
        // Arrange
        SetUser("user-1");
        _sender.Setup(s => s.Send(It.IsAny<CreateQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Question>("Invalid tags"));

        // Act
        var result = await _sut.CreateQuestion(new CreateQuestionDto("T", "C", ["bad"]));

        // Assert
        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    // ── GetQuestions ────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestions_ReturnsOkWithPaginatedResult()
    {
        // Arrange
        var pagination = new PaginationResult<Question>
        {
            Items = [new Question { Title = "T", Content = "C", AskerId = "u-1" }],
            TotalCount = 1, Page = 1, PageSize = 5
        };
        _sender.Setup(s => s.Send(It.IsAny<GetQuestionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagination);

        // Act
        var result = await _sut.GetQuestions(new QuestionsQuery());

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value!.Items.Count.ShouldBe(1);
    }

    // ── GetQuestion ────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestion_Exists_ReturnsQuestion()
    {
        // Arrange
        var question = new Question { Id = "q-1", Title = "T", Content = "C", AskerId = "u-1" };
        _sender.Setup(s => s.Send(It.IsAny<GetQuestionByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(question);

        // Act
        var result = await _sut.GetQuestion("q-1");

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value!.Id.ShouldBe("q-1");
    }

    [Fact]
    public async Task GetQuestion_NotFound_Returns404()
    {
        // Arrange
        _sender.Setup(s => s.Send(It.IsAny<GetQuestionByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Question?)null);

        // Act
        var result = await _sut.GetQuestion("nonexistent");

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    // ── DeleteQuestion ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteQuestion_Success_ReturnsNoContent()
    {
        // Arrange
        SetUser("owner-1");
        _sender.Setup(s => s.Send(It.IsAny<DeleteQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.DeleteQuestion("q-1");

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteQuestion_Forbidden_MapsForbidResult()
    {
        // Arrange
        SetUser("other-user");
        _sender.Setup(s => s.Send(It.IsAny<DeleteQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(DomainErrors.Forbidden));

        // Act
        var result = await _sut.DeleteQuestion("q-1");

        // Assert
        result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteQuestion_NotFound_Returns404()
    {
        // Arrange
        SetUser("user-1");
        _sender.Setup(s => s.Send(It.IsAny<DeleteQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(DomainErrors.QuestionNotFound));

        // Act
        var result = await _sut.DeleteQuestion("q-1");

        // Assert
        result.ShouldBeOfType<NotFoundResult>();
    }

    // ── AcceptAnswer ────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptAnswer_Success_ReturnsNoContent()
    {
        // Arrange
        SetUser("owner-1");
        _sender.Setup(s => s.Send(It.IsAny<AcceptAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.AcceptAnswer("q-1", "a-1");

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }
}