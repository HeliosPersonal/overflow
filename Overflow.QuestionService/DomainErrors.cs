namespace Overflow.QuestionService;

/// <summary>
/// Standardized error messages returned by command handlers.
/// Used by controllers to map domain failures to HTTP status codes.
/// </summary>
public static class DomainErrors
{
    public const string QuestionNotFound = "Question not found";
    public const string AnswerNotFound = "Answer not found";
    public const string NotFound = "Not found";
    public const string Forbidden = "Forbidden";
    public const string InvalidTags = "Invalid tags";
}