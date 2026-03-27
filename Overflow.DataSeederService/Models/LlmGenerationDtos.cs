using System.Text.Json.Serialization;

namespace Overflow.DataSeederService.Models;

public class AnswerGenerationDto
{
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
    [JsonPropertyName("fix_steps")] public List<string> FixSteps { get; set; } = [];
    [JsonPropertyName("code_snippet")] public string CodeSnippet { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
}

public record AnswerWithScore(AnswerGenerationDto Answer, string RenderedHtml);