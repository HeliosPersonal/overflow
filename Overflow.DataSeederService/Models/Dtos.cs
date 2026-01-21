namespace Overflow.DataSeederService.Models;

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

public class CreateProfileDto
{
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
}

public class CastVoteDto
{
    public required string TargetId { get; set; }
    public required string TargetType { get; set; } // "Question" or "Answer"
    public required string TargetUserId { get; set; } // User who created the target
    public required string QuestionId { get; set; } // Question ID (same as TargetId for questions)
    public int VoteValue { get; set; } // 1 = upvote, -1 = downvote
}

public class UserProfile
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime JoinedAt { get; set; }
    public int Reputation { get; set; }
}

public class UserProfileWithAuth
{
    public UserProfile Profile { get; set; } = new();
    public string? KeycloakUserId { get; set; }
    public string? Token { get; set; }
}

public class Question
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string AskerId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<string> TagSlugs { get; set; } = new();
    public List<Answer> Answers { get; set; } = new();
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
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public int UsageCount { get; set; }
}
