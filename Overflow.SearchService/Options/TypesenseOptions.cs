using System.ComponentModel.DataAnnotations;

namespace Overflow.SearchService.Options;

public class TypesenseOptions
{
    public const string SectionName = nameof(TypesenseOptions);

    [Required] public required string ConnectionUrl { get; set; }

    [Required] public required string ApiKey { get; set; }

    [Required] public required string CollectionName { get; set; }
}