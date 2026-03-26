using System.ComponentModel.DataAnnotations;

namespace Overflow.DataSeederService.Models;

public class AiAnswerOptions
{
    public const string SectionName = nameof(AiAnswerOptions);

    [Required] public required string QuestionServiceUrl { get; set; }

    [Required] public required string ProfileServiceUrl { get; set; }

    [Required] public required string LlmApiUrl { get; set; }

    [Required] public required string LlmModel { get; set; }

    /// <summary>Display name shown on the AI user's profile and answers.</summary>
    [Required]
    public required string AiDisplayName { get; set; }

    /// <summary>Email used for the AI Keycloak account.</summary>
    [Required]
    public required string AiEmail { get; set; }

    /// <summary>Password for the AI Keycloak account.</summary>
    [Required]
    public required string AiPassword { get; set; }

    /// <summary>Number of answer variants to generate; the best one is posted.</summary>
    [Range(1, 10)]
    public int AnswerVariants { get; set; } = 3;

    /// <summary>Max LLM retries per variant.</summary>
    [Range(0, 5)]
    public int MaxGenerationRetries { get; set; } = 2;

    /// <summary>HTTP timeout in seconds for LLM requests (model pull + chat). Default 10 minutes.</summary>
    [Range(30, 3600)]
    public int LlmTimeoutSeconds { get; set; } = 600;
}