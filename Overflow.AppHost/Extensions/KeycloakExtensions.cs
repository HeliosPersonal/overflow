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
            .WithEnvironment("KeycloakOptions__Url", options.Url)
            .WithEnvironment("KeycloakOptions__ServiceName", options.ServiceName)
            .WithEnvironment("KeycloakOptions__Realm", options.Realm)
            .WithEnvironment("KeycloakOptions__Audience", options.Audience);

        // Add valid issuers with indexed keys for proper array binding
        for (var i = 0; i < options.ValidIssuers.Count; i++)
        {
            builder.WithEnvironment($"KeycloakOptions__ValidIssuers__{i}", options.ValidIssuers[i]);
        }

        return builder;
    }
}
