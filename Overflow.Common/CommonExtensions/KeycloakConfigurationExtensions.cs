using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Overflow.Common.Options;

namespace Overflow.Common.CommonExtensions;

public static class KeycloakConfigurationExtensions
{
    /// <summary>
    /// Validates that the KeycloakOptions configuration section exists.
    /// Called early in startup so misconfiguration fails fast.
    /// </summary>
    public static IHostApplicationBuilder ConfigureKeycloakFromSettings(
        this IHostApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(KeycloakOptions.SectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"Configuration section '{KeycloakOptions.SectionName}' is missing. " +
                "Ensure KeycloakOptions is configured in appsettings.json or environment variables.");
        }

        return builder;
    }
}