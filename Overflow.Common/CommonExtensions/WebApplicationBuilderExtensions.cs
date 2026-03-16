using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Overflow.Common.CommonExtensions;

public static class WebApplicationBuilderExtensions
{
    private const string InfisicalHost = "https://eu.infisical.com";

    /// <summary>
    /// Infisical folder paths to fetch application secrets from.
    /// Secrets are stored under /app/* using SCREAMING_SNAKE_CASE naming with __ separators.
    /// Infrastructure secrets live in /infra (consumed by CI/CD only, not loaded here).
    /// </summary>
    private static readonly string[] AppSecretPaths =
    [
        "/app/connections",
        "/app/auth",
        "/app/services",
    ];

    public static IHostApplicationBuilder AddEnvVariablesAndConfigureSecrets(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddEnvironmentVariables();

        if (builder.Environment.IsDevelopment())
        {
            return builder;
        }

        var clientId = builder.Configuration[ConfigurationKeys.InfisicalClientId]
                       ?? throw new InvalidOperationException(
                           $"{ConfigurationKeys.InfisicalClientId} not found in configuration");

        var clientSecret = builder.Configuration[ConfigurationKeys.InfisicalClientSecret]
                           ?? throw new InvalidOperationException(
                               $"{ConfigurationKeys.InfisicalClientSecret} not found in configuration");

        var projectId = builder.Configuration[ConfigurationKeys.InfisicalProjectId]
                        ?? throw new InvalidOperationException(
                            $"{ConfigurationKeys.InfisicalProjectId} not found in configuration");

        var environmentSlug = builder.Environment.EnvironmentName.ToLowerInvariant();

        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger("InfisicalConfiguration");

        logger.LogInformation("Loading secrets from Infisical for environment: {Environment}", environmentSlug);

        try
        {
            var allSecrets = new List<Secret>();

            foreach (var path in AppSecretPaths)
            {
                var secrets = LoadSecretsFromInfisical(environmentSlug, clientId, clientSecret, projectId, path);
                logger.LogInformation("Loaded {Count} secrets from Infisical path: {Path}", secrets.Length, path);
                allSecrets.AddRange(secrets);
            }

            if (allSecrets.Count > 0)
            {
                var secretsDict = allSecrets.ToDictionary(
                    s => ToConfigurationKey(s.SecretKey),
                    s => s.SecretValue
                );

                builder.Configuration.AddInMemoryCollection(secretsDict!);
                logger.LogInformation("Loaded {Count} total secrets from Infisical", allSecrets.Count);

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

    /// <summary>
    /// Converts an Infisical SCREAMING_SNAKE_CASE secret key to a .NET <see cref="IConfiguration"/> key.
    /// <list type="bullet">
    ///   <item>Keys containing <c>__</c> are split into segments, each segment is PascalCased, then joined with <c>:</c>.<br/>
    ///     e.g. <c>CONNECTION_STRINGS__ESTIMATION_DB</c> → <c>ConnectionStrings:EstimationDb</c></item>
    ///   <item>Keys without <c>__</c> are returned unchanged (flat env-var style).<br/>
    ///     e.g. <c>OTEL_EXPORTER_OTLP_HEADERS</c> → <c>OTEL_EXPORTER_OTLP_HEADERS</c></item>
    /// </list>
    /// </summary>
    private static string ToConfigurationKey(string secretKey)
    {
        if (!secretKey.Contains("__"))
            return secretKey;

        return string.Join(":", secretKey
            .Split(["__"], StringSplitOptions.None)
            .Select(segment => string.Concat(
                segment.Split('_')
                       .Where(w => w.Length > 0)
                       .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant())
            )));
    }

    private static Secret[] LoadSecretsFromInfisical(string environmentSlug, string clientId, string clientSecret,
        string projectId, string secretPath)
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
            SecretPath = secretPath,
            ProjectId = projectId,
        };

        var secrets = infisicalClient.Secrets().ListAsync(options).GetAwaiter().GetResult();
        return secrets;
    }
}