using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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

        // Construct the authority URL from Url and Realm
        var authority = $"{keycloakOptions.Url}/realms/{keycloakOptions.Realm}";
        
        // Determine if we should require HTTPS metadata based on the authority URL
        var requireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"🔐 Configuring Keycloak Authentication:");
        Console.WriteLine($"   Authority: {authority}");
        Console.WriteLine($"   Audience: {keycloakOptions.Audience}");
        Console.WriteLine($"   Require HTTPS Metadata: {requireHttpsMetadata}");
        Console.WriteLine($"   Valid Issuers: {string.Join(", ", keycloakOptions.ValidIssuers)}");

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