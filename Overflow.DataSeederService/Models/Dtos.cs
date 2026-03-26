namespace Overflow.DataSeederService.Models;

// ── Requests sent to the Question Service ────────────────────────────────────

public class CreateAnswerDto
{
    public required string Content { get; set; }
}

// ── Response from the Question Service ───────────────────────────────────

public class Answer
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string UserId { get; set; } = "";
    public string QuestionId { get; set; } = "";
}

// ── AI user ──────────────────────────────────────────────────────────────────

/// <summary>Authenticated AI account used to post answers.</summary>
public class AiUser
{
    public required string KeycloakUserId { get; init; }
    public required string Email { get; init; }
    public string? Token { get; set; }
}