namespace Overflow.DataSeederService.Models;

public class CreateAnswerDto
{
    public required string Content { get; set; }
}

public class Answer
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string UserId { get; set; } = "";
    public string QuestionId { get; set; } = "";
}

public record AiUser(string KeycloakUserId, string Email, string Token);