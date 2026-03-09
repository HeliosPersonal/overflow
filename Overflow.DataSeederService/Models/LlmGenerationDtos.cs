using System.Text.Json.Serialization;
using Overflow.DataSeederService.Services;

namespace Overflow.DataSeederService.Models;

/// <summary>Topic seed for the generation pipeline (Step 1 output).</summary>
public class TopicSeedDto
{
    [JsonPropertyName("topic")] public string Topic { get; set; } = "";

    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "";

    [JsonPropertyName("problem_type")] public string ProblemType { get; set; } = "";

    [JsonPropertyName("bug_reason")] public string BugReason { get; set; } = "";

    [JsonPropertyName("key_entities")] public List<string> KeyEntities { get; set; } = new();

    [JsonPropertyName("solution_hint")] public string SolutionHint { get; set; } = "";
}

/// <summary>Structured question fields filled by the LLM (Step 2 output). <see cref="ContentAssembler" /> owns layout.</summary>
public class QuestionGenerationDto
{
    /// <summary>Plain-text title, 8–14 words.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>1–2 paragraphs describing what the dev is doing and where it fails. No code.</summary>
    [JsonPropertyName("context")]
    public string Context { get; set; } = "";

    /// <summary>Minimal reproducible example, plain code, no fences.</summary>
    [JsonPropertyName("code_example")]
    public string CodeExample { get; set; } = "";

    /// <summary>Language identifier, e.g. "csharp", "python".</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>One sentence: what the dev expected.</summary>
    [JsonPropertyName("expected_behavior")]
    public string ExpectedBehavior { get; set; } = "";

    /// <summary>One sentence: what actually happens.</summary>
    [JsonPropertyName("actual_behavior")]
    public string ActualBehavior { get; set; } = "";

    /// <summary>2–4 technology tags.</summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>Structured answer fields filled by the LLM (Step 3 output). <see cref="ContentAssembler" /> owns layout.</summary>
public class AnswerGenerationDto
{
    /// <summary>1–3 sentences on root cause. No code.</summary>
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";

    /// <summary>Ordered fix steps, 1–5 single-action sentences.</summary>
    [JsonPropertyName("fix_steps")]
    public List<string> FixSteps { get; set; } = new();

    /// <summary>Corrected code snippet, plain code, no fences.</summary>
    [JsonPropertyName("code_snippet")]
    public string CodeSnippet { get; set; } = "";

    /// <summary>Language identifier — match the question language.</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>Optional caveats, 1–2 sentences. Empty if none.</summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("accepted")] public bool Accepted { get; set; }
}

/// <summary>Critic evaluation result (Step 4 output).</summary>
public class CriticResultDto
{
    [JsonPropertyName("valid")] public bool Valid { get; set; }

    [JsonPropertyName("issues")] public List<string> Issues { get; set; } = new();
}

/// <summary>Repaired question/answer after critic flags issues (Step 5 output).</summary>
public class RepairResultDto
{
    [JsonPropertyName("question")] public QuestionGenerationDto? Question { get; set; }

    [JsonPropertyName("answer")] public AnswerGenerationDto? Answer { get; set; }
}