using System.ComponentModel.DataAnnotations;

namespace Overflow.DataSeederService.Models;

public class AiAnswerOptions
{
    public const string SectionName = nameof(AiAnswerOptions);

    [Required] public required string QuestionServiceUrl { get; set; }
    [Required] public required string ProfileServiceUrl { get; set; }
    [Required] public required string LlmApiUrl { get; set; }
    [Required] public required string LlmModel { get; set; }
    [Required] public required string AiDisplayName { get; set; }

    /// <summary>Set via Infisical: AI_ANSWER_OPTIONS__AI_EMAIL. Empty = AI answers disabled.</summary>
    public string AiEmail { get; set; } = "";

    /// <summary>Set via Infisical: AI_ANSWER_OPTIONS__AI_PASSWORD. Empty = AI answers disabled.</summary>
    public string AiPassword { get; set; } = "";

    [Required, Range(1, 10)] public required int AnswerVariants { get; set; }
    [Required, Range(0, 5)] public required int MaxGenerationRetries { get; set; }
    [Required, Range(30, 3600)] public required int LlmTimeoutSeconds { get; set; }
    [Required, Range(30, 600)] public required int GenerationTimeoutSeconds { get; set; }
    [Required, Range(10, 120)] public required int RankingTimeoutSeconds { get; set; }
}