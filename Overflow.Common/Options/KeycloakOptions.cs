namespace Overflow.Common.Options;

public class KeycloakOptions
{
    public required string? Url { get; set; }

    public required string ServiceName { get; set; }

    public required string Realm { get; set; }

    public required string Audience { get; set; }

    public required List<string> ValidIssuers { get; set; }
    
    public required string? AdminClientId { get; set; }
    
    public required string? AdminClientSecret { get; set; }
    
    public required string? NextJsClientId { get; set; }
    
    public required string? NextJsClientSecret { get; set; }
}