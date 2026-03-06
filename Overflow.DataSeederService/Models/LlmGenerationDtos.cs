using System.Text.Json.Serialization;

namespace Overflow.DataSeederService.Models;

/// <summary>
/// Structured seed describing the technical problem to generate content about.
/// Returned by Step 1 of the generation pipeline.
/// </summary>
public class TopicSeedDto
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = "";

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";

    [JsonPropertyName("problem_type")]
    public string ProblemType { get; set; } = "";

    [JsonPropertyName("bug_reason")]
    public string BugReason { get; set; } = "";

    [JsonPropertyName("key_entities")]
    public List<string> KeyEntities { get; set; } = new();

    [JsonPropertyName("solution_hint")]
    public string SolutionHint { get; set; } = "";
}

/// <summary>
/// Structured StackOverflow-style question with discrete semantic fields.
/// The LLM fills each field with raw text only.
/// <see cref="ContentAssembler"/> assembles the final formatted markdown —
/// guaranteeing consistent layout regardless of model output quality.
/// Returned by Step 2 of the generation pipeline.
/// </summary>
public class QuestionGenerationDto
{
    /// <summary>Concise StackOverflow-style title (8–14 words, plain text, no formatting).</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>
    /// 1–2 short paragraphs (separated by \n\n) describing what the developer
    /// is trying to do and where the problem occurs. No code here.
    /// </summary>
    [JsonPropertyName("context")]
    public string Context { get; set; } = "";

    /// <summary>Minimal reproducible code example (5–20 lines). Plain code only — no fences.</summary>
    [JsonPropertyName("code_example")]
    public string CodeExample { get; set; } = "";

    /// <summary>
    /// Programming language identifier for syntax highlighting
    /// (e.g. "python", "csharp", "javascript", "go").
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>One sentence: what the developer expected to happen.</summary>
    [JsonPropertyName("expected_behavior")]
    public string ExpectedBehavior { get; set; } = "";

    /// <summary>One sentence: what actually happens instead.</summary>
    [JsonPropertyName("actual_behavior")]
    public string ActualBehavior { get; set; } = "";

    /// <summary>Relevant technology tags (2–4 items).</summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Structured StackOverflow-style answer with discrete semantic fields.
/// The LLM fills each field with raw text only.
/// <see cref="ContentAssembler"/> assembles the final formatted markdown.
/// Returned by Step 3 of the generation pipeline.
/// </summary>
public class AnswerGenerationDto
{
    /// <summary>1–3 sentences explaining the root cause of the problem. No code here.</summary>
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";

    /// <summary>
    /// Ordered list of step-by-step fix instructions.
    /// Each item is a single action sentence. 1–5 steps.
    /// </summary>
    [JsonPropertyName("fix_steps")]
    public List<string> FixSteps { get; set; } = new();

    /// <summary>The corrected code snippet (plain code only — no fences, no inline explanation).</summary>
    [JsonPropertyName("code_snippet")]
    public string CodeSnippet { get; set; } = "";

    /// <summary>
    /// Programming language identifier for syntax highlighting
    /// (e.g. "python", "csharp", "javascript"). Match the question language.
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>Optional additional tips or caveats (1–2 sentences). Empty string if not needed.</summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }
}

/// <summary>Result of the critic evaluation pass (Step 4).</summary>
public class CriticResultDto
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = new();
}

/// <summary>Repaired question and/or answer after the critic flagged issues (Step 5).</summary>
public class RepairResultDto
{
    [JsonPropertyName("question")]
    public QuestionGenerationDto? Question { get; set; }

    [JsonPropertyName("answer")]
    public AnswerGenerationDto? Answer { get; set; }
}
