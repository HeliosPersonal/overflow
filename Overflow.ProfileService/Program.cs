using System.Text.Json.Serialization;
using Overflow.Common.CommonExtensions;
using Overflow.Common.Options;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Middleware;
using Overflow.ProfileService.Options;
using Overflow.ProfileService.Services;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();
builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();
builder.AddNpgsqlDbContext<ProfileDbContext>("profileDb");
builder.Services.AddCommandFlow(typeof(Program).Assembly);

// ── Keycloak admin for anonymous user cleanup ──
builder.Services
    .AddOptions<KeycloakOptions>()
    .BindConfiguration(KeycloakOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<AnonymousCleanupOptions>()
    .BindConfiguration(AnonymousCleanupOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<KeycloakAdminClient>();
builder.Services.AddHostedService<AnonymousUserCleanupService>();

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