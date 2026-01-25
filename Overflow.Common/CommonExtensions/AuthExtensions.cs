using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Overflow.Common.Options;

namespace Overflow.Common.CommonExtensions;

public static class AuthExtensions
{
    public static WebApplicationBuilder AddKeyCloakAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<KeycloakOptions>()
            .BindConfiguration(nameof(KeycloakOptions))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var keycloakOptions = builder
            .Services
            .BuildServiceProvider()
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakOptions>>().Value;

        var authority = $"{keycloakOptions.Url}/realms/{keycloakOptions.Realm}";
        var requireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger("KeycloakConfiguration");
        
        logger.LogInformation("Configuring Keycloak authentication: Authority={Authority}, Audience={Audience}, RequireHttps={RequireHttps}", 
            authority, keycloakOptions.Audience, requireHttpsMetadata);
        logger.LogDebug("Valid issuers: {ValidIssuers}", string.Join(", ", keycloakOptions.ValidIssuers));

        builder.Services
            .AddAuthentication()
            .AddKeycloakJwtBearer(
                keycloakOptions.ServiceName,
                keycloakOptions.Realm,
                options =>
                {
                    options.Authority = authority;
                    options.RequireHttpsMetadata = requireHttpsMetadata;
                    options.Audience = keycloakOptions.Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuers = keycloakOptions.ValidIssuers,
                        ClockSkew = TimeSpan.Zero
                    };
                });

        builder.Services.AddAuthorizationBuilder();

        return builder;
    }
}