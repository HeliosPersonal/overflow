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
}