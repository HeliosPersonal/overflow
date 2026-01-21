namespace Overflow.DataSeederService.Models;

public class SeederOptions
{
    public required string QuestionServiceUrl { get; set; }
    public required string ProfileServiceUrl { get; set; }
    public required string VoteServiceUrl { get; set; }
    public required string LlmApiUrl { get; set; } 
    public required string LlmModel { get; set; }
    public int IntervalMinutes { get; set; } 
    public int MinAnswersPerQuestion { get; set; } 
    public int MaxAnswersPerQuestion { get; set; }
    public int MinUsersToGenerate { get; set; } 
    public int MaxUsersToGenerate { get; set; }
    public bool EnableLlmGeneration { get; set; }
    public bool EnableVoting { get; set; } 
}
