using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Overflow.Common.CommonExtensions;
using Overflow.Contracts;
using Overflow.Contracts.Helpers;
using Overflow.ServiceDefaults;
using Overflow.VoteService.Data;
using Overflow.VoteService.DTOs;
using Overflow.VoteService.Models;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<VoteDbContext>()
    .AddRabbitMqHealthCheck();

await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });
builder.AddNpgsqlDbContext<VoteDbContext>("voteDb");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/votes", async (
    CastVoteDto dto, 
    VoteDbContext db, 
    ClaimsPrincipal user, 
    IMessageBus bus,
    ILogger<Program> logger) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
    {
        logger.LogWarning("Vote cast attempted without user ID");
        return Results.Unauthorized();
    }

    if (dto.TargetType is not ("Question" or "Answer"))
    {
        logger.LogWarning("Invalid vote target type: {TargetType} for user {UserId}", dto.TargetType, userId);
        return Results.BadRequest("Invalid target type");
    }

    var alreadyVoted = await db.Votes.AnyAsync(x => x.UserId == userId && x.TargetId == dto.TargetId);
    if (alreadyVoted)
    {
        logger.LogDebug("Duplicate vote attempt: {UserId} already voted on {TargetType} {TargetId}", 
            userId, dto.TargetType, dto.TargetId);
        return Results.BadRequest("Already voted");
    }

    db.Votes.Add(new Vote
    {
        TargetId = dto.TargetId,
        TargetType = dto.TargetType,
        UserId = userId,
        VoteValue = dto.VoteValue,
        QuestionId = dto.QuestionId
    });

    await db.SaveChangesAsync();

    var reason = (dto.VoteValue, dto.TargetType) switch
    {
        (1, "Question") => ReputationReason.QuestionUpvoted,
        (1, "Answer") => ReputationReason.AnswerUpvoted,
        (-1, "Answer") => ReputationReason.AnswerDownvoted,
        _ => ReputationReason.QuestionDownvoted
    };

    await bus.PublishAsync(ReputationHelper.MakeEvent(dto.TargetUserId, reason, userId));
    await bus.PublishAsync(new VoteCasted(dto.TargetId, dto.TargetType, dto.VoteValue));
    
    logger.LogInformation("Vote cast: {UserId} voted {VoteValue} on {TargetType} {TargetId}", 
        userId, dto.VoteValue, dto.TargetType, dto.TargetId);

    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/votes/{questionId}", async (
    string questionId, 
    VoteDbContext db, 
    ClaimsPrincipal user,
    ILogger<Program> logger) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
    {
        logger.LogWarning("Get votes attempted without user ID");
        return Results.Unauthorized();
    }
    
    var votes = await db.Votes
        .Where(x => x.UserId == userId && x.QuestionId == questionId)
        .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
        .ToListAsync();
    
    logger.LogDebug("Retrieved {Count} votes for user {UserId} on question {QuestionId}", 
        votes.Count, userId, questionId);
    
    return Results.Ok(votes);
}).RequireAuthorization();

app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<VoteDbContext>();

app.Run();