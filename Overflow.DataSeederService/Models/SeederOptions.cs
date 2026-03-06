namespace Overflow.DataSeederService.Models;

public class SeederOptions
{
    public required string QuestionServiceUrl { get; set; }
    public required string ProfileServiceUrl { get; set; }
    public required string VoteServiceUrl { get; set; }
    public required string LlmApiUrl { get; set; }
    public required string LlmModel { get; set; }
    public required int IntervalMinutes { get; set; }
    public required int MinAnswersPerQuestion { get; set; }
    public required int MaxAnswersPerQuestion { get; set; }
    public required int MaxSeederUsers { get; set; }
    public required string SeederUsernamePrefix { get; set; }
    public required bool EnableLlmGeneration { get; set; }
    public required bool EnableVoting { get; set; }

    /// <summary>Maximum JSON parse/generation retry attempts per pipeline step.</summary>
    public int MaxGenerationRetries { get; set; } = 2;

    /// <summary>Run the critic evaluation pass after answer generation.</summary>
    public bool EnableCriticPass { get; set; } = true;

    /// <summary>Run the repair pass when the critic flags issues.</summary>
    public bool EnableRepairPass { get; set; } = true;
}