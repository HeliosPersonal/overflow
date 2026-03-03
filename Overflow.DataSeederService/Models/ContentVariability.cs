namespace Overflow.DataSeederService.Models;

/// <summary>
/// Defines the variability dimensions for generated content to ensure diversity.
/// Kept intentionally simple so small LLMs (3B params) can follow the instructions reliably.
/// </summary>
public record ContentVariability
{
    public ContentLength Length { get; init; }
    public AnswerStyle Style { get; init; }

    /// <summary>
    /// Generate a random variability profile for questions.
    /// </summary>
    public static ContentVariability RandomForQuestion()
    {
        return new ContentVariability
        {
            Length = Pick<ContentLength>(),
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

public enum AnswerStyle
{
    Neutral,
    Conversational,
    Formal,
    StepByStep,
    CodeHeavy
}
