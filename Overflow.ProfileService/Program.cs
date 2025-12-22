using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Overflow.Common.CommonExtensions;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.DTOs;
using Overflow.ProfileService.Middleware;
using Overflow.ServiceDefaults;


var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();
builder.AddNpgsqlDbContext<ProfileDbContext>("profileDb");

// Add health checks
builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<ProfileDbContext>()
    .AddRabbitMqHealthCheck();

builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseMiddleware<UserProfileCreationMiddleware>();

app.MapGet("/profiles/me", async (ClaimsPrincipal user, ProfileDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var profile = await db.UserProfiles.FindAsync(userId);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
}).RequireAuthorization();

app.MapGet("/profiles/batch", async (string ids, ProfileDbContext db) =>
{
    var list = ids.Split(",", StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

    var rows = await db.UserProfiles
        .Where(x => list.Contains(x.Id))
        .Select(x => new ProfileSummaryDto(x.Id, x.DisplayName, x.Reputation))
        .ToListAsync();

    return Results.Ok(rows);
});

app.MapGet("profiles", async (string? sortBy, ProfileDbContext db) =>
{
    var query = db.UserProfiles.AsQueryable();

    query = sortBy == "reputation"
        ? query.OrderByDescending(x => x.Reputation)
        : query.OrderBy(x => x.DisplayName);

    return await query.ToListAsync();
});

app.MapGet("profiles/{id}", async (string id, ProfileDbContext db) =>
{
    var profile = await db.UserProfiles.FindAsync(id);

    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapPut("/profiles/edit", async (EditProfileDto dto, ClaimsPrincipal user, ProfileDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();

    profile.DisplayName = dto.DisplayName ?? profile.DisplayName;
    profile.Description = dto.Description ?? profile.Description;

    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization();

app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<ProfileDbContext>();

app.Run();