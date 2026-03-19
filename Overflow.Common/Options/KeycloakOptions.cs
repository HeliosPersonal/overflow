using System.ComponentModel.DataAnnotations;

namespace Overflow.Common.Options;

public class KeycloakOptions
{
    public const string SectionName = nameof(KeycloakOptions);

    [Required] public required string? Url { get; set; }

    [Required] public required string ServiceName { get; set; }

    [Required] public required string Realm { get; set; }

    [Required] public required string Audience { get; set; }

    [Required, MinLength(1)] public required List<string> ValidIssuers { get; set; }

    public required string? AdminClientId { get; set; }

    public required string? AdminClientSecret { get; set; }

    public required string? NextJsClientId { get; set; }

    public required string? NextJsClientSecret { get; set; }
}