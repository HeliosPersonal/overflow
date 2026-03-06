using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var identity = context.Principal?.Identity as ClaimsIdentity;
                            if (identity is null) return Task.CompletedTask;

                            // Map Keycloak realm_access.roles to ClaimTypes.Role
                            var realmAccessClaim = identity.FindFirst("realm_access");
                            if (realmAccessClaim is not null)
                            {
                                try
                                {
                                    var realmAccess = JsonDocument.Parse(realmAccessClaim.Value);
                                    if (realmAccess.RootElement.TryGetProperty("roles", out var roles))
                                    {
                                        foreach (var role in roles.EnumerateArray())
                                        {
                                            var roleValue = role.GetString();
                                            if (roleValue is not null)
                                                identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                                        }
                                    }
                                }
                                catch
                                {
                                    // ignore malformed claim
                                }
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

        builder.Services.AddAuthorizationBuilder();

        return builder;
    }
}