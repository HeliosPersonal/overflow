namespace Overflow.DataSeederService.Models;

public class SeederOptions
{
    public required string QuestionServiceUrl { get; set; }
    public required string ProfileServiceUrl { get; set; }
    public required string VoteServiceUrl { get; set; }
    public required string LlmApiUrl { get; set; }
    public required string LlmModel { get; set; }

    public int QuestionIntervalMinutes { get; set; }
    public int AnswerIntervalMinutes { get; set; }
    public int AcceptIntervalMinutes { get; set; }
    public int AnswerStartDelayMinutes { get; set; }
    public int AcceptStartDelayMinutes { get; set; }
    public required int MaxSeederUsers { get; set; }
    public required string SeederUsernamePrefix { get; set; }
    public required string SeederUserPassword { get; set; }
    public required bool EnableVoting { get; set; }
    public int MaxGenerationRetries { get; set; }
    public bool EnableCriticPass { get; set; }
    public bool EnableRepairPass { get; set; }
    public int MinAnswerPoolSize { get; set; }
    public int AcceptQuestionsPerRun { get; set; }
    public int AcceptDelayMinMs { get; set; }
    public int AcceptDelayMaxMs { get; set; }
}