using Microsoft.Extensions.Configuration;
using Overflow.Common.Options;

namespace Overflow.AppHost.Extensions;

public static class KeycloakExtensions
{
    public static IResourceBuilder<ProjectResource> WithKeycloakOptions(
        this IResourceBuilder<ProjectResource> builder,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("KeycloakOptions")
            .Get<KeycloakOptions>();

        if (options == null)
        {
            throw new InvalidOperationException("KeycloakOptions configuration is missing or invalid.");
        }

        builder
            .WithEnvironment("KEYCLOAK_OPTIONS__URL", options.Url)
            .WithEnvironment("KEYCLOAK_OPTIONS__SERVICE_NAME", options.ServiceName)
            .WithEnvironment("KEYCLOAK_OPTIONS__REALM", options.Realm)
            .WithEnvironment("KEYCLOAK_OPTIONS__AUDIENCE", options.Audience);

        // Add valid issuers with indexed keys for proper array binding
        for (var i = 0; i < options.ValidIssuers.Count; i++)
        {
            builder.WithEnvironment($"KEYCLOAK_OPTIONS__VALID_ISSUERS__{i}", options.ValidIssuers[i]);
        }

        return builder;
    }
}