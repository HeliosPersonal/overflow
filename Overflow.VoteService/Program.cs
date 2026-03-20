using Overflow.Common.CommonExtensions;
using Overflow.ServiceDefaults;
using Overflow.VoteService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddControllers();
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
app.MapControllers();

app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<VoteDbContext>();

app.Run();