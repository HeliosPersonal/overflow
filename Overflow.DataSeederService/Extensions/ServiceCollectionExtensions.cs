using Microsoft.Extensions.Options;
using OllamaSharp;
using Overflow.Common.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;
using Refit;

namespace Overflow.DataSeederService.Extensions;

internal static class ServiceCollectionExtensions
{
    private const int RefitTimeoutSeconds = 60;

    public static IServiceCollection AddOllamaClient(this IServiceCollection services)
    {
        services.AddSingleton<IOllamaApiClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AiAnswerOptions>>().Value;
            var http = new HttpClient
            {
                BaseAddress = new Uri(opts.LlmApiUrl),
                Timeout = TimeSpan.FromSeconds(opts.LlmTimeoutSeconds)
            };
            return new OllamaApiClient(http, opts.LlmModel);
        });
        return services;
    }

    public static IServiceCollection AddKeycloakClients(this IServiceCollection services)
    {
        services.AddTransient<AdminBearerTokenHandler>();

        services.AddRefitClient<IKeycloakTokenClient>()
            .ConfigureHttpClient((sp, c) =>
            {
                var kc = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                c.BaseAddress = new Uri($"{kc.Url}/realms/{kc.Realm}");
                c.Timeout = TimeSpan.FromSeconds(RefitTimeoutSeconds);
            })
            .AddDataSeederResilienceHandler();

        services.AddRefitClient<IKeycloakAdminClient>()
            .ConfigureHttpClient((sp, c) =>
            {
                var kc = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                c.BaseAddress = new Uri($"{kc.Url}/admin/realms/{kc.Realm}");
                c.Timeout = TimeSpan.FromSeconds(RefitTimeoutSeconds);
            })
            .AddHttpMessageHandler<AdminBearerTokenHandler>()
            .AddDataSeederResilienceHandler();

        return services;
    }

    public static IServiceCollection AddBackendApiClients(this IServiceCollection services)
    {
        services.AddRefitClient<IQuestionApiClient>()
            .ConfigureHttpClient((sp, c) =>
            {
                var opts = sp.GetRequiredService<IOptions<AiAnswerOptions>>().Value;
                c.BaseAddress = new Uri(opts.QuestionServiceUrl);
                c.Timeout = TimeSpan.FromSeconds(RefitTimeoutSeconds);
            })
            .AddDataSeederResilienceHandler();

        services.AddRefitClient<IProfileApiClient>()
            .ConfigureHttpClient((sp, c) =>
            {
                var opts = sp.GetRequiredService<IOptions<AiAnswerOptions>>().Value;
                c.BaseAddress = new Uri(opts.ProfileServiceUrl);
                c.Timeout = TimeSpan.FromSeconds(RefitTimeoutSeconds);
            })
            .AddDataSeederResilienceHandler();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<KeycloakAdminService>();
        services.AddSingleton<AiUserProvider>();
        services.AddSingleton<LlmService>();
        services.AddScoped<AiAnswerService>();
        services.AddHostedService<AiUserBootstrapService>();
        return services;
    }
}