using System.Text.Json.Serialization;

namespace Overflow.DataSeederService.Models;

public class AnswerGenerationDto
{
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
    [JsonPropertyName("points")] public List<string> Points { get; set; } = [];
    [JsonPropertyName("code_snippet")] public string CodeSnippet { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "";
}

public record AnswerWithScore(AnswerGenerationDto Answer, string RenderedHtml);