using System.Text;
using System.Text.RegularExpressions;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>
/// Assembles and validates final HTML content from structured LLM-generated DTOs.
///
/// Design principle:
///   The LLM provides raw semantic values (context text, plain code, single sentences).
///   This class owns all layout decisions — every seeded post has a consistent,
///   realistic StackOverflow-like structure regardless of model output variance.
///
///   LLM → semantic fields → ContentAssembler → deterministic markdown → SanitizeHtml → HTML
/// </summary>
public static class ContentAssembler
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Question assembly
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Assembles a StackOverflow-style question body and converts it to HTML.
    ///
    /// Output layout (markdown before HTML conversion):
    ///
    ///   {context paragraphs}
    ///
    ///   ```lang
    ///   {code_example}
    ///   ```
    ///
    ///   **Expected behaviour:** {expected_behavior}
    ///
    ///   **Actual behaviour:** {actual_behavior}
    /// </summary>
    public static string BuildQuestionHtml(QuestionGenerationDto dto)
    {
        var md = new StringBuilder();

        // Context — normalise whitespace and enforce paragraph spacing
        if (!string.IsNullOrWhiteSpace(dto.Context))
        {
            var context = NormaliseText(dto.Context);
            md.AppendLine(context);
        }

        // Code block — the service always provides the fences; the LLM never does
        if (!string.IsNullOrWhiteSpace(dto.CodeExample))
        {
            var lang = NormaliseLanguage(dto.Language);
            md.AppendLine();
            md.AppendLine($"```{lang}");
            md.AppendLine(dto.CodeExample.Trim());
            md.AppendLine("```");
        }

        // Expected / Actual behaviour
        var hasExpected = !string.IsNullOrWhiteSpace(dto.ExpectedBehavior);
        var hasActual   = !string.IsNullOrWhiteSpace(dto.ActualBehavior);

        if (hasExpected || hasActual)
        {
            md.AppendLine();
            if (hasExpected)
                md.AppendLine($"**Expected behaviour:** {dto.ExpectedBehavior.Trim()}");
            if (hasActual)
                md.AppendLine($"**Actual behaviour:** {dto.ActualBehavior.Trim()}");
        }

        var markdown = CollapseBlankLines(md.ToString().Trim());
        return LlmClient.SanitizeHtml(markdown);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Answer assembly
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Assembles a StackOverflow-style answer body and converts it to HTML.
    ///
    /// Output layout (markdown before HTML conversion):
    ///
    ///   {explanation}
    ///
    ///   1. {fix_steps[0]}
    ///   2. {fix_steps[1]}
    ///   ...
    ///
    ///   ```lang
    ///   {code_snippet}
    ///   ```
    ///
    ///   {notes}   (omitted when empty)
    /// </summary>
    public static string BuildAnswerHtml(AnswerGenerationDto dto)
    {
        var md = new StringBuilder();

        // Root-cause explanation paragraph
        if (!string.IsNullOrWhiteSpace(dto.Explanation))
        {
            md.AppendLine(NormaliseText(dto.Explanation));
        }

        // Numbered fix steps
        var steps = dto.FixSteps.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (steps.Count > 0)
        {
            md.AppendLine();
            for (int i = 0; i < steps.Count; i++)
                md.AppendLine($"{i + 1}. {steps[i].Trim()}");
        }

        // Fix code block — fences always added by the service
        if (!string.IsNullOrWhiteSpace(dto.CodeSnippet))
        {
            var lang = NormaliseLanguage(dto.Language);
            md.AppendLine();
            md.AppendLine($"```{lang}");
            md.AppendLine(dto.CodeSnippet.Trim());
            md.AppendLine("```");
        }

        // Optional notes (only if non-trivial)
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            md.AppendLine();
            md.AppendLine(NormaliseText(dto.Notes));
        }

        var markdown = CollapseBlankLines(md.ToString().Trim());
        return LlmClient.SanitizeHtml(markdown);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Validation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that a QuestionGenerationDto meets the minimum quality bar
    /// required before the content is sent to the Question Service.
    ///
    /// Returns a list of human-readable issues (empty = valid).
    /// </summary>
    public static List<string> ValidateQuestion(QuestionGenerationDto? dto)
    {
        var issues = new List<string>();
        if (dto == null) { issues.Add("DTO is null"); return issues; }

        // Title: 8–14 words, plain text
        var titleWords = dto.Title.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (string.IsNullOrWhiteSpace(dto.Title))
            issues.Add("title is empty");
        else if (titleWords < 3)
            issues.Add($"title too short ({titleWords} words, minimum 3)");
        else if (titleWords > 20)
            issues.Add($"title too long ({titleWords} words, maximum 20)");

        if (string.IsNullOrWhiteSpace(dto.Context))
            issues.Add("context is empty");

        if (string.IsNullOrWhiteSpace(dto.CodeExample))
            issues.Add("code_example is empty");
        else
        {
            var codeLines = dto.CodeExample.Trim().Split('\n').Length;
            if (codeLines > 30)
                issues.Add($"code_example is too long ({codeLines} lines, maximum 30)");
        }

        if (string.IsNullOrWhiteSpace(dto.ExpectedBehavior))
            issues.Add("expected_behavior is empty");

        if (string.IsNullOrWhiteSpace(dto.ActualBehavior))
            issues.Add("actual_behavior is empty");

        // Detect UI contamination strings
        foreach (var uiPhrase in UiContaminationPhrases)
        {
            if (ContainsUiPhrase(dto.Context, uiPhrase) ||
                ContainsUiPhrase(dto.Title, uiPhrase))
            {
                issues.Add($"UI contamination detected: \"{uiPhrase}\"");
            }
        }

        return issues;
    }

    /// <summary>
    /// Validates that an AnswerGenerationDto meets the minimum quality bar.
    /// Returns a list of human-readable issues (empty = valid).
    /// </summary>
    public static List<string> ValidateAnswer(AnswerGenerationDto? dto)
    {
        var issues = new List<string>();
        if (dto == null) { issues.Add("DTO is null"); return issues; }

        if (string.IsNullOrWhiteSpace(dto.Explanation))
            issues.Add("explanation is empty");

        if (string.IsNullOrWhiteSpace(dto.CodeSnippet))
            issues.Add("code_snippet is empty");
        else
        {
            var codeLines = dto.CodeSnippet.Trim().Split('\n').Length;
            if (codeLines > 30)
                issues.Add($"code_snippet is too long ({codeLines} lines, maximum 30)");
        }

        // Detect filler phrases
        foreach (var filler in FillerPhrases)
        {
            if (ContainsUiPhrase(dto.Explanation, filler) ||
                ContainsUiPhrase(dto.Notes, filler))
            {
                issues.Add($"filler phrase detected: \"{filler}\"");
            }
        }

        return issues;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Markdown post-processing
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Collapses 3+ consecutive blank lines to exactly 2, and trims trailing spaces per line.
    /// </summary>
    public static string CollapseBlankLines(string text)
    {
        // Trim trailing whitespace on each line
        text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);
        // Collapse 3+ blank lines → 2
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalises a text field: trims, collapses internal excess blank lines,
    /// and strips inline markdown fences the model may have accidentally inserted.
    /// </summary>
    private static string NormaliseText(string text)
    {
        text = text.Trim();
        // Remove accidental fenced code blocks from prose fields
        text = Regex.Replace(text, @"```[a-zA-Z]*\r?\n[\s\S]*?```", "[code removed — use code_example field]");
        // Collapse excess internal blank lines
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        // Trim trailing whitespace per line
        text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);
        return text;
    }

    private static bool ContainsUiPhrase(string text, string phrase) =>
        text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps model-returned language strings to valid fenced-code-block identifiers.
    /// Falls back to empty string (unhighlighted plain text) for unknown values.
    /// </summary>
    public static string NormaliseLanguage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Trim().ToLowerInvariant() switch
        {
            "c#" or "csharp" or "cs"      => "csharp",
            "js" or "javascript"          => "javascript",
            "ts" or "typescript"          => "typescript",
            "py" or "python"              => "python",
            "rb" or "ruby"                => "ruby",
            "go" or "golang"              => "go",
            "rs" or "rust"                => "rust",
            "java"                        => "java",
            "kotlin" or "kt"              => "kotlin",
            "swift"                       => "swift",
            "cpp" or "c++"                => "cpp",
            "c"                           => "c",
            "php"                         => "php",
            "sh" or "bash" or "shell"     => "bash",
            "ps" or "powershell"          => "powershell",
            "sql"                         => "sql",
            "html"                        => "html",
            "css"                         => "css",
            "yaml" or "yml"               => "yaml",
            "json"                        => "json",
            "xml"                         => "xml",
            "dockerfile"                  => "dockerfile",
            _                             => ""
        };
    }

    private static readonly string[] UiContaminationPhrases =
    [
        "answers", "viewed", "asked today", "asked", "modified", "highest score",
        "active", "votes", "vote", "reputation", "badge", "gold", "silver", "bronze",
        "linked", "related", "hot network", "sidebar"
    ];

    private static readonly string[] FillerPhrases =
    [
        "hope this helps", "let me know if", "thanks in advance", "feel free to",
        "happy to help", "hope that helps", "please let me know", "good luck",
        "try restarting", "hope it works"
    ];
}
