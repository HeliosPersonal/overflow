using System.Text.Json.Serialization;

namespace Overflow.DataSeederService.Models;

/// <summary>Structured answer fields filled by the LLM. Used to build the final HTML answer.</summary>
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
}

/// <summary>An answer variant with its rendered HTML, used for LLM-based ranking.</summary>
public class AnswerWithScore
{
    public required AnswerGenerationDto Answer { get; init; }
    public required string RenderedHtml { get; init; }
}