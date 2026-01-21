using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Overflow.Common.CommonExtensions;

public static class WebApplicationBuilderExtensions
{
    private const string InfisicalHost = "https://eu.infisical.com";
    private const string SecretPath = "/";

    /// <summary>
    /// Configures the application to load secrets from Infisical based on the current environment.
    /// Secrets are loaded and added to the configuration, overriding any existing values.
    /// Credentials are read from IConfiguration (supports User Secrets, env vars, appsettings).
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddEnvVariablesAndConfigureSecrets(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddEnvironmentVariables();

        if (builder.Environment.IsDevelopment())
        {
            return builder;
        }
        
        // Get Infisical credentials from configuration (supports User Secrets, env vars, appsettings)
        var clientId = builder.Configuration["INFISICAL_CLIENT_ID"]
                       ?? throw new InvalidOperationException(
                           "INFISICAL_CLIENT_ID not found in configuration. Add it to User Secrets or environment variables.");

        var clientSecret = builder.Configuration["INFISICAL_CLIENT_SECRET"]
                           ?? throw new InvalidOperationException(
                               "INFISICAL_CLIENT_SECRET not found in configuration. Add it to User Secrets or environment variables.");

        var projectId = builder.Configuration["INFISICAL_PROJECT_ID"]
                        ?? throw new InvalidOperationException(
                            "INFISICAL_PROJECT_ID not found in configuration. Add it to User Secrets or environment variables.");

        var environmentSlug = builder.Environment.EnvironmentName.ToLowerInvariant();

        Console.WriteLine($"🔐 Loading secrets from Infisical (Environment: {environmentSlug})...");

        try
        {
            var secrets = LoadSecretsFromInfisical(environmentSlug, clientId, clientSecret, projectId);

            if (secrets.Length > 0)
            {
                // Convert secret keys from environment variable format to configuration format
                // Example: KeycloakOptions__ClientId → KeycloakOptions:ClientId
                var secretsDict = secrets.ToDictionary(
                    s => s.SecretKey.Replace("__", ":"),
                    s => s.SecretValue
                );

                builder.Configuration.AddInMemoryCollection(secretsDict!);

                Console.WriteLine($"✅ Loaded {secrets.Length} secrets from Infisical");

                // Debug: Show loaded configuration keys
                if (!builder.Environment.IsProduction())
                {
                    foreach (var key in secretsDict.Keys.OrderBy(k => k))
                    {
                        Console.WriteLine($"   📋 {key}");
                    }
                }
            }
            else
            {
                Console.WriteLine("⚠️  No secrets found in Infisical");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to load secrets from Infisical: {ex.Message}");

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