using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Templates;

/// <summary>
/// Holds the system + user prompt pair and generation parameters for an LLM request.
/// </summary>
public record LlmPrompt(string SystemPrompt, string UserPrompt, int MaxTokens = 500, double Temperature = 0.7);

/// <summary>
/// Centralized LLM prompt templates. Kept simple for small models (3B params).
/// The model writes Markdown; LlmClient.SanitizeHtml converts it to HTML.
/// </summary>
public static class LlmPrompts
{
    // ─────────────────────────────────────────────
    //  Question (Title + Body in a single call)
    // ─────────────────────────────────────────────

    public static LlmPrompt QuestionTitleAndContent(string tag, ContentVariability v)
    {
        var (lengthHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => ("2-3 sentences, no code.", 300),
            ContentLength.Medium => ("4-6 sentences with 1 short code snippet.", 600),
            ContentLength.Long => ("8-12 sentences with 2 code snippets.", 900),
            _ => ("4-6 sentences with 1 short code snippet.", 600)
        };

        var system =
            $"You are a developer writing a question on Stack Overflow about {tag}. " +
            "Write in Markdown. Be realistic and specific.";

        var user =
            $"Write a Stack Overflow question about {tag}.\n\n" +
            "Use EXACTLY this format:\n" +
            "===TITLE===\n" +
            "A short plain-text title (no formatting)\n" +
            "===BODY===\n" +
            "The question body in Markdown\n\n" +
            $"Length: {lengthHint}\n" +
            "The body MUST be about the same topic as the title.";

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: 0.8);
    }

    // ─────────────────────────────────────────────
    //  Fallback: separate title call
    // ─────────────────────────────────────────────

    public static LlmPrompt QuestionTitle(string tag)
    {
        var system = "You generate Stack Overflow question titles. Output only the title text, nothing else.";
        var user = $"Write a realistic Stack Overflow question title about {tag}. Plain text, 15-100 characters.";
        return new LlmPrompt(system, user, MaxTokens: 60, Temperature: 0.8);
    }

    // ─────────────────────────────────────────────
    //  Fallback: separate content call
    // ─────────────────────────────────────────────

    public static LlmPrompt QuestionContent(string title, string tag)
    {
        var v = ContentVariability.RandomForQuestion();

        var (lengthHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => ("2-3 sentences, no code.", 300),
            ContentLength.Medium => ("4-6 sentences with 1 code snippet.", 600),
            ContentLength.Long => ("8-12 sentences with 2 code snippets.", 900),
            _ => ("4-6 sentences with 1 code snippet.", 600)
        };

        var system = "You write Stack Overflow question bodies in Markdown.";

        var user =
            $"Write the question body for: \"{title}\"\n" +
            $"Topic: {tag}\n" +
            $"Length: {lengthHint}\n" +
            "Stay on the same topic as the title. Use Markdown.";

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: 0.7);
    }

    // ─────────────────────────────────────────────
    //  Answer
    // ─────────────────────────────────────────────

    public static LlmPrompt Answer(string questionTitle, string questionContent)
    {
        var v = ContentVariability.RandomForAnswer();

        var (lengthHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => ("1-3 sentences. Very concise.", 200),
            ContentLength.Medium => ("4-6 sentences with explanation.", 500),
            ContentLength.Long => ("8-12 sentences, thorough with code examples.", 800),
            _ => ("4-6 sentences with explanation.", 500)
        };

        var styleHint = v.Style switch
        {
            AnswerStyle.Conversational => "Friendly, casual tone.",
            AnswerStyle.Formal => "Professional, precise tone.",
            AnswerStyle.StepByStep => "Use a numbered list of steps.",
            AnswerStyle.CodeHeavy => "Lead with a code example, keep text minimal.",
            _ => "Helpful and clear."
        };

        var system =
            "You are an experienced developer answering a Stack Overflow question. " +
            "Write in Markdown. Answer ONLY the specific question asked.";

        var user =
            $"Question: {questionTitle}\n\n" +
            $"{questionContent}\n\n" +
            $"Write an answer. {styleHint}\n" +
            $"Length: {lengthHint}";

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: 0.6);
    }

    // ─────────────────────────────────────────────
    //  Best Answer Selection
    // ─────────────────────────────────────────────

    public static LlmPrompt SelectBestAnswer(string questionTitle, List<string> answers)
    {
        var answersText = string.Join("\n\n---\n\n", answers.Select((a, i) => $"Answer {i}: {a}"));

        var system = "You evaluate technical answers. Respond with ONLY a number.";
        var user =
            $"Question: {questionTitle}\n\n{answersText}\n\n" +
            $"Which answer (0-{answers.Count - 1}) is best? Reply with the number only.";

        return new LlmPrompt(system, user, MaxTokens: 5, Temperature: 0.1);
    }
}
