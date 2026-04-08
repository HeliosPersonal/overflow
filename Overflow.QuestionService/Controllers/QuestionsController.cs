using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Common;
using Overflow.Common.CommonExtensions;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Features.Questions.Commands;
using Overflow.QuestionService.Features.Questions.Queries;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.RequestHelpers;

namespace Overflow.QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class QuestionsController(ISender sender) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
    {
        var userId = User.GetRequiredUserId();
        var result = await sender.Send(new CreateQuestionCommand(dto.Title, dto.Content, dto.Tags, userId));
        return result.IsSuccess
            ? Created($"/questions/{result.Value.Id}", result.Value)
            : BadRequest(result.Error);
    }

    [HttpGet]
    public async Task<ActionResult<PaginationResult<Question>>> GetQuestions([FromQuery] QuestionsQuery q)
    {
        return await sender.Send(new GetQuestionsQuery(q));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await sender.Send(new GetQuestionByIdQuery(id));
        return question is null ? NotFound() : question;
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var userId = User.GetRequiredUserId();
        var result = await sender.Send(new UpdateQuestionCommand(id, dto.Title, dto.Content, dto.Tags, userId));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteQuestion(string id)
    {
        var userId = User.GetRequiredUserId();
        var isAdmin = User.IsInRole("admin");
        var result = await sender.Send(new DeleteQuestionCommand(id, userId, isAdmin));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [Authorize]
    [HttpPost("{questionId}/answers")]
    public async Task<ActionResult> PostAnswer(string questionId, CreateAnswerDto dto)
    {
        var userId = User.GetRequiredUserId();
        var result = await sender.Send(new PostAnswerCommand(questionId, dto.Content, userId));
        return result.IsSuccess
            ? Created($"/questions/{questionId}", result.Value)
            : MapFailure(result.Error);
    }

    [Authorize]
    [HttpPut("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
    {
        var result = await sender.Send(new UpdateAnswerCommand(questionId, answerId, dto.Content));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [Authorize]
    [HttpDelete("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    {
        var userId = User.GetRequiredUserId();
        var isAdmin = User.IsInRole("admin");
        var result = await sender.Send(new DeleteAnswerCommand(questionId, answerId, userId, isAdmin));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [Authorize]
    [HttpPost("{questionId}/answers/{answerId}/accept")]
    public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
    {
        var result = await sender.Send(new AcceptAnswerCommand(questionId, answerId));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    private ActionResult MapFailure(string error) => error switch
    {
        DomainErrors.Forbidden => Forbid(),
        DomainErrors.QuestionNotFound or DomainErrors.AnswerNotFound or DomainErrors.NotFound => NotFound(),
        _ => BadRequest(error)
    };
}