namespace Overflow.DataSeederService.Models;

// ── Requests sent to the Question / Vote services ────────────────────────────

public class CreateQuestionDto
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public required List<string> Tags { get; set; }
}

public class CreateAnswerDto
{
    public required string Content { get; set; }
}

public class CastVoteDto
{
    public required string TargetId { get; set; }
    public required string TargetType { get; set; } // "Question" | "Answer"
    public required string TargetUserId { get; set; } // author of the target
    public required string QuestionId { get; set; }
    public int VoteValue { get; set; } // 1 = upvote, -1 = downvote
}

// ── Responses from the Question Service ──────────────────────────────────────

public class Question
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string AskerId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<string> TagSlugs { get; set; } = new();
    public List<Answer> Answers { get; set; } = new();
    public bool HasAcceptedAnswer { get; set; }
}

public class Answer
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string UserId { get; set; } = "";
    public string QuestionId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class Tag
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
}

// ── Seeder user pool ─────────────────────────────────────────────────────────

/// <summary>Authenticated seeder account. Token is refreshed on demand via <see cref="Services.SeederUserPool" />.</summary>
public class SeederUser(string keycloakUserId, string displayName, string token)
{
    public string KeycloakUserId { get; } = keycloakUserId;
    public string DisplayName { get; } = displayName;
    public string Token { get; set; } = token;
}