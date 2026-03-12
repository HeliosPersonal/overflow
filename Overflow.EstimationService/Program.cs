using System.Text.Json;
using Overflow.Common.CommonExtensions;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Extensions;
using Overflow.EstimationService.Services;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── EF Core + PostgreSQL ─────────────────────────────────────────────────
builder.AddNpgsqlDbContext<EstimationDbContext>("estimationDb");

// ── Services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<EstimationRoomService>();
builder.Services.AddSingleton<WebSocketBroadcaster>();

// ── Profile Service (display name resolution) ───────────────────────────
builder.Services.AddHttpClient<ProfileServiceClient>(client =>
{
    var profileUrl = builder.Configuration["PROFILE_SERVICE_URL"]
                     ?? builder.Configuration["services:profile-svc:https:0"]
                     ?? builder.Configuration["services:profile-svc:http:0"]
                     ?? "http://localhost:8001";
    client.BaseAddress = new Uri(profileUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IdentityResolver>();

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<EstimationDbContext>();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets(
    new WebSocketOptions
    {
        KeepAliveInterval = TimeSpan.FromSeconds(30)
    });

app.MapControllers();
app.MapEstimationWebSocket();
app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<EstimationDbContext>();

app.Run();