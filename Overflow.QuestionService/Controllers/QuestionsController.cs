using System.Security.Claims;
using FastExpressionCompiler;
using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.Contracts.Helpers;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.RequestHelpers;
using Overflow.QuestionService.Services;
using Wolverine;

namespace Overflow.QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class QuestionsController(
    QuestionDbContext db, 
    IMessageBus bus, 
    TagService tagService,
    ILogger<QuestionsController> logger) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");
        
        if (userId is null || name is null)
        {
            logger.LogWarning("Question creation attempted without valid user claims");
            return BadRequest("Cannot get user details");
        }

        if (!await tagService.AreTagsValidAsync(dto.Tags))
        {
            logger.LogWarning("Question creation failed: invalid tags {Tags} for user {UserId}", 
                string.Join(", ", dto.Tags), userId);
            return BadRequest("Invalid tags");
        }
        
        var sanitizer = new HtmlSanitizer();
        var question = new Question
        {
            Title = dto.Title,
            Content = sanitizer.Sanitize(dto.Content),
            TagSlugs = dto.Tags,
            AskerId = userId
        };
        
        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            db.Questions.Add(question);
            await db.SaveChangesAsync();
        
            await bus.PublishAsync(new QuestionCreated(question.Id, question.Title, question.Content, 
                question.CreatedAt, question.TagSlugs));
            
            await tx.CommitAsync();
            
            logger.LogInformation("Question created: {QuestionId} by {UserId} with tags {Tags}", 
                question.Id, userId, string.Join(", ", dto.Tags));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Failed to create question for user {UserId}", userId);
            throw;
        }
        
        var slugs = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (slugs.Length > 0)
        {
            await db.Tags
                .Where(t => slugs.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, t => t.UsageCount + 1)); 
        }
        
        return Created($"/questions/{question.Id}", question);
    }

    [HttpGet]
    public async Task<ActionResult<PaginationResult<Question>>> GetQuestions([FromQuery]QuestionsQuery q)
    {
        logger.LogDebug("Fetching questions: Sort={Sort}, Tag={Tag}, Page={Page}", q.Sort, q.Tag, q.Page);
        
        var query = db.Questions.AsQueryable(); 

        if (!string.IsNullOrEmpty(q.Tag))
        {
            query = query.Where(x => x.TagSlugs.Contains(q.Tag));
        }

        query = q.Sort switch
        {
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            "active" => query.OrderByDescending(x => new[]
            {
                x.CreatedAt,
                x.UpdatedAt ?? DateTime.MinValue,
                x.Answers.Max(a => (DateTime?)a.CreatedAt) ?? DateTime.MinValue,
                x.Answers.Max(a => a.UpdatedAt) ?? DateTime.MinValue,
            }.Max()),
            "unanswered" => query.Where(x => x.AnswerCount == 0).OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var result = await query.ToPaginatedListAsync(q);
        
        logger.LogDebug("Returned {Count} questions out of {TotalCount}", result.Items.Count, result.TotalCount);
        return result;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await db.Questions
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == id);
        
        if (question is null)
        {
            logger.LogDebug("Question not found: {QuestionId}", id);
            return NotFound();
        }
        
        await db.Questions.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));
        
        logger.LogDebug("Question viewed: {QuestionId}, ViewCount={ViewCount}", id, question.ViewCount + 1);
        return question;
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null)
        {
            logger.LogWarning("Update failed: Question {QuestionId} not found", id);
            return NotFound();
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId)
        {
            logger.LogWarning("Update forbidden: User {UserId} attempted to update question {QuestionId} owned by {OwnerId}", 
                userId, id, question.AskerId);
            return Forbid();
        }
        
        if (!await tagService.AreTagsValidAsync(dto.Tags))
        {
            logger.LogWarning("Update failed: Invalid tags {Tags} for question {QuestionId}", 
                string.Join(", ", dto.Tags), id);
            return BadRequest("Invalid tags");
        }
        
        var original = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var incoming = dto.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var removed = original.Except(incoming, StringComparer.OrdinalIgnoreCase).ToArray();
        var added = incoming.Except(original, StringComparer.OrdinalIgnoreCase).ToArray();
        
        var sanitizer = new HtmlSanitizer();
        question.Title = dto.Title;
        question.Content = sanitizer.Sanitize(dto.Content);
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();

        if (removed.Length > 0)
        {
            await db.Tags
                .Where(t => removed.Contains(t.Slug) && t.UsageCount > 0)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, t => t.UsageCount - 1));
        }
        
        if (added.Length > 0)
        {
            await db.Tags
                .Where(t => added.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, t => t.UsageCount + 1));
        }
        
        await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title, question.Content, 
            question.TagSlugs.AsArray()));
        
        logger.LogInformation("Question updated: {QuestionId} by {UserId}, tags added: {Added}, removed: {Removed}", 
            id, userId, string.Join(", ", added), string.Join(", ", removed));
        
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteQuestion(string id)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null)
        {
            logger.LogWarning("Delete failed: Question {QuestionId} not found", id);
            return NotFound();
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId)
        {
            logger.LogWarning("Delete forbidden: User {UserId} attempted to delete question {QuestionId} owned by {OwnerId}", 
                userId, id, question.AskerId);
            return Forbid();
        }
        
        db.Questions.Remove(question);
        await db.SaveChangesAsync();
        await bus.PublishAsync(new QuestionDeleted(question.Id));
        
        logger.LogInformation("Question deleted: {QuestionId} by {UserId}", id, userId);
        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{questionId}/answers")]
    public async Task<ActionResult> PostAnswer(string questionId, CreateAnswerDto dto)
    {
        var question = await db.Questions.FindAsync(questionId);
        if (question is null)
        {
            logger.LogWarning("Answer post failed: Question {QuestionId} not found", questionId);
            return NotFound();
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");
        
        if (userId is null || name is null)
        {
            logger.LogWarning("Answer post attempted without valid user claims");
            return BadRequest("Cannot get user details");
        }

        var sanitizer = new HtmlSanitizer();
        var answer = new Answer
        {
            Content = sanitizer.Sanitize(dto.Content),
            UserId = userId,
            QuestionId = questionId
        };
        
        question.Answers.Add(answer);
        question.AnswerCount++;
        await db.SaveChangesAsync();
        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));
        
        logger.LogInformation("Answer posted: {AnswerId} to question {QuestionId} by {UserId}", 
            answer.Id, questionId, userId);
        
        return Created($"/questions/{questionId}", answer);
    }
    
    [Authorize]
    [HttpPut("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
    {
        var answer = await db.Answers.FindAsync(answerId);
        if (answer is null)
        {
            logger.LogWarning("Answer update failed: Answer {AnswerId} not found", answerId);
            return NotFound();
        }
        
        if (answer.QuestionId != questionId)
        {
            logger.LogWarning("Answer update failed: Answer {AnswerId} does not belong to question {QuestionId}", 
                answerId, questionId);
            return BadRequest("Cannot update answer details");
        }
        
        var sanitizer = new HtmlSanitizer();
        answer.Content = sanitizer.Sanitize(dto.Content);
        answer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        
        logger.LogDebug("Answer updated: {AnswerId} in question {QuestionId}", answerId, questionId);
        return NoContent();
    }
    
    [Authorize]
    [HttpDelete("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    {
        var answer = await db.Answers.FindAsync(answerId);
        var question = await db.Questions.FindAsync(questionId);
        
        if (answer is null || question is null)
        {
            logger.LogWarning("Answer delete failed: Answer {AnswerId} or Question {QuestionId} not found", 
                answerId, questionId);
            return NotFound();
        }
        
        if (answer.QuestionId != questionId || answer.Accepted)
        {
            logger.LogWarning("Answer delete forbidden: Answer {AnswerId} is accepted or doesn't match question {QuestionId}", 
                answerId, questionId);
            return BadRequest("Cannot delete this answer");
        }
        
        db.Answers.Remove(answer);
        question.AnswerCount--;
        await db.SaveChangesAsync();
        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));
        
        logger.LogInformation("Answer deleted: {AnswerId} from question {QuestionId}", answerId, questionId);
        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{questionId}/answers/{answerId}/accept")]
    public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
    {
        var answer = await db.Answers.FindAsync(answerId);
        var question = await db.Questions.FindAsync(questionId);
        
        if (answer is null || question is null)
        {
            logger.LogWarning("Answer accept failed: Answer {AnswerId} or Question {QuestionId} not found", 
                answerId, questionId);
            return NotFound();
        }
        
        if (answer.QuestionId != questionId || question.HasAcceptedAnswer)
        {
            logger.LogWarning("Answer accept failed: Mismatch or already has accepted answer. Question={QuestionId}, Answer={AnswerId}", 
                questionId, answerId);
            return BadRequest("Cannot accept answer");
        }

        answer.Accepted = true;
        question.HasAcceptedAnswer = true;
        await db.SaveChangesAsync();
        await bus.PublishAsync(new AnswerAccepted(questionId));
        await bus.PublishAsync(ReputationHelper.MakeEvent(answer.UserId, 
            ReputationReason.AnswerAccepted, question.AskerId));
        
        logger.LogInformation("Answer accepted: {AnswerId} for question {QuestionId}, user {UserId} gained reputation", 
            answerId, questionId, answer.UserId);
        
        return NoContent();
    }
}