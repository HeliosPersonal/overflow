using Overflow.Common.CommonExtensions;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Middleware;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();
builder.AddNpgsqlDbContext<ProfileDbContext>("profileDb");

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<ProfileDbContext>()
    .AddRabbitMqHealthCheck();

await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserProfileCreationMiddleware>();
app.MapControllers();

app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<ProfileDbContext>();

app.Run();