using Microsoft.Extensions.Options;
using Overflow.Common.CommonExtensions;
using Overflow.SearchService.Data;
using Overflow.SearchService.Extensions;
using Overflow.SearchService.Options;
using Overflow.ServiceDefaults;
using Typesense;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddTypesenseConfiguration(builder.Configuration);
builder.Services.AddCommandFlow(typeof(Program).Assembly);

builder.Services.AddHealthChecks()
    .AddTypesenseHealthCheck()
    .AddRabbitMqHealthCheck();

await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapDefaultEndpoints();

using var scope = app.Services.CreateScope();
var typesenseClient = scope.ServiceProvider.GetRequiredService<ITypesenseClient>();
var typesenseOptions = scope.ServiceProvider.GetRequiredService<IOptions<TypesenseOptions>>().Value;
await SearchInitializer.EnsureIndexExists(typesenseClient, typesenseOptions.CollectionName);

app.Run();