using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>Converts structured LLM DTOs into HTML. The LLM fills semantic fields; this class owns all layout.</summary>
public static class ContentAssembler
{
    private const int MinQuestionHtmlLength = 300;
    private const int MinAnswerHtmlLength = 150;

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

    /// <summary>Assembles a question DTO into HTML.</summary>
    public static string BuildQuestionHtml(QuestionGenerationDto dto, ILogger? logger = null)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(dto.Context))
            sb.Append("<p>").Append(dto.Context.Trim()).Append("</p>");

        if (!string.IsNullOrWhiteSpace(dto.CodeExample))
        {
            var lang = NormaliseLanguage(dto.Language);
            sb.Append($"<h3>Code example</h3><pre><code class=\"language-{lang}\">")
              .Append(System.Net.WebUtility.HtmlEncode(dto.CodeExample.Trim()))
              .Append("</code></pre>");
        }

        if (!string.IsNullOrWhiteSpace(dto.ExpectedBehavior))
            sb.Append("<h3>Expected behavior</h3><p>").Append(dto.ExpectedBehavior.Trim()).Append("</p>");

        if (!string.IsNullOrWhiteSpace(dto.ActualBehavior))
            sb.Append("<h3>Actual behavior</h3><p>").Append(dto.ActualBehavior.Trim()).Append("</p>");

        var html = sb.ToString();
        logger?.LogDebug("[Render] Question HTML ({Length} chars)", html.Length);
        return html;
    }

    /// <summary>Assembles an answer DTO into HTML.</summary>
    public static string BuildAnswerHtml(AnswerGenerationDto dto, ILogger? logger = null)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(dto.Explanation))
            sb.Append("<p>").Append(dto.Explanation.Trim()).Append("</p>");

        var steps = dto.FixSteps.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (steps.Count > 0)
        {
            sb.Append("<h3>Fix</h3><ol>");
            foreach (var step in steps)
                sb.Append("<li>").Append(step.Trim()).Append("</li>");
            sb.Append("</ol>");
        }

        if (!string.IsNullOrWhiteSpace(dto.CodeSnippet))
        {
            var lang = NormaliseLanguage(dto.Language);
            sb.Append($"<pre><code class=\"language-{lang}\">")
              .Append(System.Net.WebUtility.HtmlEncode(dto.CodeSnippet.Trim()))
              .Append("</code></pre>");
        }

        if (!string.IsNullOrWhiteSpace(dto.Notes))
            sb.Append("<h3>Notes</h3><p>").Append(dto.Notes.Trim()).Append("</p>");

        var html = sb.ToString();
        logger?.LogDebug("[Render] Answer HTML ({Length} chars)", html.Length);
        return html;
    }

    /// <summary>Returns quality issues for a question DTO. Empty = valid.</summary>
    public static List<string> ValidateQuestion(QuestionGenerationDto? dto)
    {
        var issues = new List<string>();
        if (dto == null) { issues.Add("DTO is null"); return issues; }

        var titleWords = dto.Title.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (string.IsNullOrWhiteSpace(dto.Title))
            issues.Add("title is empty");
        else if (titleWords < 3)
            issues.Add($"title too short ({titleWords} words, minimum 3)");
        else if (titleWords > 20)
            issues.Add($"title too long ({titleWords} words, maximum 20)");

        if (string.IsNullOrWhiteSpace(dto.Context))       issues.Add("context is empty");
        if (string.IsNullOrWhiteSpace(dto.CodeExample))   issues.Add("code_example is empty");
        else
        {
            var lines = dto.CodeExample.Trim().Split('\n').Length;
            if (lines > 30) issues.Add($"code_example too long ({lines} lines, max 30)");
        }
        if (string.IsNullOrWhiteSpace(dto.ExpectedBehavior)) issues.Add("expected_behavior is empty");
        if (string.IsNullOrWhiteSpace(dto.ActualBehavior))   issues.Add("actual_behavior is empty");

        foreach (var phrase in UiContaminationPhrases)
            if (ContainsPhrase(dto.Context, phrase) || ContainsPhrase(dto.Title, phrase))
                issues.Add($"UI contamination: \"{phrase}\"");

        return issues;
    }

    /// <summary>Returns false if rendered question HTML is structurally incomplete.</summary>
    public static bool ValidateRenderedQuestion(string html, QuestionGenerationDto dto, out string error)
    {
        if (html.Length < MinQuestionHtmlLength)
        {
            error = $"rendered HTML too short ({html.Length} chars, min {MinQuestionHtmlLength})";
            return false;
        }
        if (!html.Contains("<pre><code") && !string.IsNullOrWhiteSpace(dto.CodeExample))
        {
            error = "rendered HTML missing <pre><code> despite non-empty code_example";
            return false;
        }
        error = "";
        return true;
    }

    /// <summary>Returns quality issues for an answer DTO. Empty = valid.</summary>
    public static List<string> ValidateAnswer(AnswerGenerationDto? dto)
    {
        var issues = new List<string>();
        if (dto == null) { issues.Add("DTO is null"); return issues; }

        if (string.IsNullOrWhiteSpace(dto.Explanation)) issues.Add("explanation is empty");
        if (string.IsNullOrWhiteSpace(dto.CodeSnippet)) issues.Add("code_snippet is empty");
        else
        {
            var lines = dto.CodeSnippet.Trim().Split('\n').Length;
            if (lines > 30) issues.Add($"code_snippet too long ({lines} lines, max 30)");
        }

        foreach (var phrase in FillerPhrases)
            if (ContainsPhrase(dto.Explanation, phrase) || ContainsPhrase(dto.Notes, phrase))
                issues.Add($"filler phrase: \"{phrase}\"");

        return issues;
    }

    /// <summary>Returns false if rendered answer HTML is structurally incomplete.</summary>
    public static bool ValidateRenderedAnswer(string html, AnswerGenerationDto dto, out string error)
    {
        if (html.Length < MinAnswerHtmlLength)
        {
            error = $"rendered HTML too short ({html.Length} chars, min {MinAnswerHtmlLength})";
            return false;
        }
        if (!html.Contains("<pre><code") && !string.IsNullOrWhiteSpace(dto.CodeSnippet))
        {
            error = "rendered HTML missing <pre><code> despite non-empty code_snippet";
            return false;
        }
        error = "";
        return true;
    }

    /// <summary>Normalises LLM language identifiers to valid highlight.js class names.</summary>
    public static string NormaliseLanguage(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "c#" or "csharp" or "cs"       => "csharp",
            "js" or "javascript"            => "javascript",
            "ts" or "typescript"            => "typescript",
            "py" or "python"                => "python",
            "rb" or "ruby"                  => "ruby",
            "go" or "golang"                => "go",
            "rs" or "rust"                  => "rust",
            "java"                          => "java",
            "kotlin" or "kt"                => "kotlin",
            "swift"                         => "swift",
            "cpp" or "c++"                  => "cpp",
            "c"                             => "c",
            "php"                           => "php",
            "sh" or "bash" or "shell"       => "bash",
            "ps" or "powershell"            => "powershell",
            "sql"                           => "sql",
            "html"                          => "html",
            "css"                           => "css",
            "yaml" or "yml"                 => "yaml",
            "json"                          => "json",
            "xml"                           => "xml",
            "dockerfile"                    => "dockerfile",
            _                               => ""
        };

    private static bool ContainsPhrase(string text, string phrase) =>
        text.Contains(phrase, StringComparison.OrdinalIgnoreCase);
}