using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Overflow.Common.CommonExtensions;

public static class WebApplicationBuilderExtensions
{
    private const string InfisicalHost = "https://eu.infisical.com";
    private const string SecretPath = "/";

    public static IHostApplicationBuilder AddEnvVariablesAndConfigureSecrets(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddEnvironmentVariables();

        if (builder.Environment.IsDevelopment())
        {
            return builder;
        }
        
        var clientId = builder.Configuration["INFISICAL_CLIENT_ID"]
                       ?? throw new InvalidOperationException(
                           "INFISICAL_CLIENT_ID not found in configuration");

        var clientSecret = builder.Configuration["INFISICAL_CLIENT_SECRET"]
                           ?? throw new InvalidOperationException(
                               "INFISICAL_CLIENT_SECRET not found in configuration");

        var projectId = builder.Configuration["INFISICAL_PROJECT_ID"]
                        ?? throw new InvalidOperationException(
                            "INFISICAL_PROJECT_ID not found in configuration");

        var environmentSlug = builder.Environment.EnvironmentName.ToLowerInvariant();
        
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger("InfisicalConfiguration");
        
        logger.LogInformation("Loading secrets from Infisical for environment: {Environment}", environmentSlug);

        try
        {
            var secrets = LoadSecretsFromInfisical(environmentSlug, clientId, clientSecret, projectId);

            if (secrets.Length > 0)
            {
                var secretsDict = secrets.ToDictionary(
                    s => s.SecretKey.Replace("__", ":"),
                    s => s.SecretValue
                );

                builder.Configuration.AddInMemoryCollection(secretsDict!);
                logger.LogInformation("Loaded {Count} secrets from Infisical", secrets.Length);

                if (!builder.Environment.IsProduction())
                {
                    logger.LogDebug("Loaded configuration keys: {Keys}", 
                        string.Join(", ", secretsDict.Keys.OrderBy(k => k)));
                }
            }
            else
            {
                logger.LogWarning("No secrets found in Infisical");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load secrets from Infisical");

            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("Failed to load required secrets from Infisical", ex);
            }
        }

        return builder;
    }

    private static Secret[] LoadSecretsFromInfisical(string environmentSlug, string clientId, string clientSecret,
        string projectId)
    {
        var settings = new InfisicalSdkSettingsBuilder()
            .WithHostUri(InfisicalHost)
            .Build();

        var infisicalClient = new InfisicalClient(settings);

        infisicalClient
            .Auth()
            .UniversalAuth()
            .LoginAsync(clientId, clientSecret)
            .GetAwaiter()
            .GetResult();

        var options = new ListSecretsOptions
        {
            SetSecretsAsEnvironmentVariables = true,
            EnvironmentSlug = environmentSlug,
            SecretPath = SecretPath,
            ProjectId = projectId,
        };

        var secrets = infisicalClient.Secrets().ListAsync(options).GetAwaiter().GetResult();
        return secrets;
    }
}