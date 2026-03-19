using System.Text.Json.Serialization;
using FluentEmail.Mailgun;
using Overflow.Common.CommonExtensions;
using Overflow.NotificationService.Auth;
using Overflow.NotificationService.Channels;
using Overflow.NotificationService.Options;
using Overflow.NotificationService.Templates;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();
builder.AddNotificationApiKeyAuth();

// Controllers + JSON enum serialization
builder.Services
    .AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });
builder.Services.AddOpenApi();

// Template renderer
builder.Services.AddSingleton<ITemplateRenderer, TemplateRenderer>();

// FluentEmail + Mailgun sender
builder.Services
    .AddOptions<MailgunOptions>()
    .BindConfiguration(MailgunOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

var mailgun = builder.Configuration.GetSection(MailgunOptions.SectionName).Get<MailgunOptions>();

var mailgunRegion = mailgun!.Region.Equals("US", StringComparison.OrdinalIgnoreCase)
    ? MailGunRegion.USA
    : MailGunRegion.EU;

builder.Services
    .AddFluentEmail(mailgun.FromEmail, mailgun.FromName)
    .AddMailGunSender(mailgun.Domain, mailgun.ApiKey, mailgunRegion);

// Channels
builder.Services.AddTransient<INotificationChannel, MailgunEmailChannel>();

// To add a new channel (e.g. Telegram):
// builder.Services.AddTransient<INotificationChannel, TelegramChannel>();

// Health checks
builder.Services.AddHealthChecks()
    .AddRabbitMqHealthCheck();

// Wolverine + RabbitMQ
await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();