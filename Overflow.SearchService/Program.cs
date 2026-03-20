using Overflow.Common.CommonExtensions;
using Overflow.SearchService.Data;
using Overflow.SearchService.Extensions;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddTypesenseConfiguration(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddTypesenseHealthCheck()
    .AddRabbitMqHealthCheck();

await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapDefaultEndpoints();

using var scope = app.Services.CreateScope();
var typesenseClient = scope.ServiceProvider.GetRequiredService<Typesense.ITypesenseClient>();
var typesenseOptions = scope.ServiceProvider
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<Overflow.SearchService.Options.TypesenseOptions>>().Value;
await SearchInitializer.EnsureIndexExists(typesenseClient, typesenseOptions.CollectionName);

app.Run();