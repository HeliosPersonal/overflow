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

        builder.Services
            .AddAuthentication()
            .AddKeycloakJwtBearer(
                keycloakOptions.ServiceName,
                keycloakOptions.Realm,
                options =>
                {
                    options.Authority = authority;
                    options.RequireHttpsMetadata = false;
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