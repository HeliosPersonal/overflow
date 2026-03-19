using System.ComponentModel.DataAnnotations;

namespace Overflow.DataSeederService.Models;

public class SeederOptions
{
    public const string SectionName = nameof(SeederOptions);

    [Required] public required string QuestionServiceUrl { get; set; }

    [Required] public required string ProfileServiceUrl { get; set; }

    [Required] public required string VoteServiceUrl { get; set; }

    [Required] public required string LlmApiUrl { get; set; }

    [Required] public required string LlmModel { get; set; }

    [Range(1, int.MaxValue)] public int QuestionIntervalMinutes { get; set; }

    [Range(1, int.MaxValue)] public int AnswerIntervalMinutes { get; set; }

    [Range(1, int.MaxValue)] public int AcceptIntervalMinutes { get; set; }

    [Range(0, int.MaxValue)] public int AnswerStartDelayMinutes { get; set; }

    [Range(0, int.MaxValue)] public int AcceptStartDelayMinutes { get; set; }

    [Range(1, int.MaxValue)] public required int MaxSeederUsers { get; set; }

    [Required] public required string SeederUsernamePrefix { get; set; }

    [Required] public required string SeederUserPassword { get; set; }

    public required bool EnableVoting { get; set; }

    [Range(0, int.MaxValue)] public int MaxGenerationRetries { get; set; }

    public bool EnableCriticPass { get; set; }

    public bool EnableRepairPass { get; set; }

    [Range(0, int.MaxValue)] public int MinAnswerPoolSize { get; set; }

    [Range(1, int.MaxValue)] public int AcceptQuestionsPerRun { get; set; }

    [Range(0, int.MaxValue)] public int AcceptDelayMinMs { get; set; }

    [Range(0, int.MaxValue)] public int AcceptDelayMaxMs { get; set; }
}