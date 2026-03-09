namespace Overflow.DataSeederService.Models;

/// <summary>Content complexity level passed as a hint to the LLM.</summary>
public enum ComplexityLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3
}

public enum AnswerStyle
{
    StepByStep,
    CodeHeavy,
    Conversational,
    Formal
}

public static class ComplexityLevelExtensions
{
    public static string ToPromptLabel(this ComplexityLevel level)
    {
        return level switch
        {
            ComplexityLevel.Beginner => "beginner",
            ComplexityLevel.Intermediate => "intermediate",
            ComplexityLevel.Advanced => "advanced",
            _ => "intermediate"
        };
    }

    public static ComplexityLevel Random()
    {
        return (ComplexityLevel)System.Random.Shared.Next(1, 4);
    }

    public static AnswerStyle RandomStyle()
    {
        var values = Enum.GetValues<AnswerStyle>();
        return values[System.Random.Shared.Next(values.Length)];
    }
}