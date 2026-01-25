using Overflow.SearchService.Options;
using Typesense.Setup;

namespace Overflow.SearchService.Extensions;

public static class TypesenseExtensions
{
    public static IServiceCollection AddTypesenseConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Load and validate Typesense options
        var typesenseOptions = configuration
            .GetSection(nameof(TypesenseOptions))
            .Get<TypesenseOptions>() ?? throw new InvalidOperationException("Typesense configuration not found");

        if (string.IsNullOrWhiteSpace(typesenseOptions.ConnectionUrl))
        {
            throw new InvalidOperationException("Typesense ConnectionUrl not configured");
        }

        if (string.IsNullOrEmpty(typesenseOptions.ApiKey))
        {
            throw new InvalidOperationException("Typesense API key not found in config");
        }

        // Register options as singleton
        services.AddSingleton(typesenseOptions);

        // Configure Typesense client
        var uri = new Uri(typesenseOptions.ConnectionUrl);
        services.AddTypesenseClient(config =>
        {
            config.ApiKey = typesenseOptions.ApiKey;
            config.Nodes = new List<Node>
            {
                new(uri.Host, uri.Port.ToString(), uri.Scheme)
            };
        });

        return services;
    }
}

