namespace Overflow.Contracts;

public static class VoteTargetType
{
    public const string Question = "Question";
    public const string Answer = "Answer";

    public static bool IsValid(string targetType) =>
        targetType is Question or Answer;
}