using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Overflow.Common.Options;

namespace Overflow.Common.CommonExtensions;

public static class KeycloakConfigurationExtensions
{
    /// <summary>
    /// Configures Keycloak options from configuration and applies them to the application builder.
    /// This ensures that Keycloak configuration from appsettings.json is properly loaded.
    /// </summary>
    public static IHostApplicationBuilder ConfigureKeycloakFromSettings(
        this IHostApplicationBuilder builder)
    {
        // Explicitly bind KeycloakOptions from configuration
        var keycloakSection = builder.Configuration.GetSection("KeycloakOptions");
        
        if (keycloakSection.Exists())
        {
            var options = keycloakSection.Get<KeycloakOptions>();
            
            if (options != null)
            {
                // Re-apply configuration as environment variables to ensure proper binding
                // This helps when configuration comes from appsettings.json files in Docker
                builder.Configuration["KeycloakOptions:Url"] = options.Url;
                builder.Configuration["KeycloakOptions:ServiceName"] = options.ServiceName;
                builder.Configuration["KeycloakOptions:Realm"] = options.Realm;
                builder.Configuration["KeycloakOptions:Audience"] = options.Audience;
                
                // Handle ValidIssuers array
                for (var i = 0; i < options.ValidIssuers.Count; i++)
                {
                    builder.Configuration[$"KeycloakOptions:ValidIssuers:{i}"] = options.ValidIssuers[i];
                }
                
                if (!string.IsNullOrEmpty(options.ClientId))
                {
                    builder.Configuration["KeycloakOptions:ClientId"] = options.ClientId;
                }
                
                if (!string.IsNullOrEmpty(options.ClientSecret))
                {
                    builder.Configuration["KeycloakOptions:ClientSecret"] = options.ClientSecret;
                }
                
                if (!string.IsNullOrEmpty(options.AdminClientId))
                {
                    builder.Configuration["KeycloakOptions:AdminClientId"] = options.AdminClientId;
                }
                
                if (!string.IsNullOrEmpty(options.AdminClientSecret))
                {
                    builder.Configuration["KeycloakOptions:AdminClientSecret"] = options.AdminClientSecret;
                }
                
                if (!string.IsNullOrEmpty(options.NextJsClientId))
                {
                    builder.Configuration["KeycloakOptions:NextJsClientId"] = options.NextJsClientId;
                }
                
                if (!string.IsNullOrEmpty(options.NextJsClientSecret))
                {
                    builder.Configuration["KeycloakOptions:NextJsClientSecret"] = options.NextJsClientSecret;
                }
            }
        }
        
        return builder;
    }
}

