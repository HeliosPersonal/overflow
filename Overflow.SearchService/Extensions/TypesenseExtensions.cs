using Overflow.SearchService.Options;
using Typesense.Setup;

namespace Overflow.SearchService.Extensions;

public static class TypesenseExtensions
{
    public static IServiceCollection AddTypesenseConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register and validate Typesense options on startup
        services
            .AddOptions<TypesenseOptions>()
            .BindConfiguration(TypesenseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Eagerly resolve options from configuration for Typesense client registration
        var typesenseOptions = configuration
                                   .GetSection(TypesenseOptions.SectionName)
                                   .Get<TypesenseOptions>()
                               ?? throw new InvalidOperationException(
                                   $"'{TypesenseOptions.SectionName}' configuration section is missing or empty. " +
                                   "Ensure TypesenseOptions__ConnectionUrl, TypesenseOptions__ApiKey, and " +
                                   "TypesenseOptions__CollectionName are set.");

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