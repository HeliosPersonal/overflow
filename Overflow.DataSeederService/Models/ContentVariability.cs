namespace Overflow.DataSeederService.Models;

/// <summary>
/// Defines the variability dimensions for generated content to ensure diversity.
/// </summary>
public record ContentVariability
{
    public ContentLength Length { get; init; }
    public ContentDepth Depth { get; init; }
    public ContentComplexity Complexity { get; init; }
    public AnswerStyle Style { get; init; }

    /// <summary>
    /// Generate a random variability profile for questions.
    /// </summary>
    public static ContentVariability RandomForQuestion()
    {
        return new ContentVariability
        {
            Length = Pick<ContentLength>(),
            Depth = Pick<ContentDepth>(),
            Complexity = Pick<ContentComplexity>(),
            Style = AnswerStyle.Neutral // not used for questions
        };
    }

    /// <summary>
    /// Generate a random variability profile for answers.
    /// </summary>
    public static ContentVariability RandomForAnswer()
    {
        return new ContentVariability
        {
            Length = Pick<ContentLength>(),
            Depth = Pick<ContentDepth>(),
            Complexity = Pick<ContentComplexity>(),
            Style = Pick<AnswerStyle>()
        };
    }

    private static T Pick<T>() where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        return values[Random.Shared.Next(values.Length)];
    }
}

public enum ContentLength
{
    Short,
    Medium,
    Long
}

public enum ContentDepth
{
    Beginner,
    Intermediate,
    Expert
}

public enum ContentComplexity
{
    Simple,
    Moderate,
    Complex
}

public enum AnswerStyle
{
    Neutral,
    Conversational,
    Formal,
    ProsAndCons,
    StepByStep,
    CodeHeavy,
    Opinionated
}

