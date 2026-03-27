using System.Net;
using System.Text;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public static class AnswerHtmlRenderer
{
    private const int MinHtmlLength = 150;
    private const int MaxCodeLines = 40;

    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = "csharp", ["csharp"] = "csharp", ["cs"] = "csharp",
        ["js"] = "javascript", ["javascript"] = "javascript",
        ["ts"] = "typescript", ["typescript"] = "typescript",
        ["py"] = "python", ["python"] = "python",
        ["rb"] = "ruby", ["ruby"] = "ruby",
        ["go"] = "go", ["golang"] = "go",
        ["rs"] = "rust", ["rust"] = "rust",
        ["java"] = "java",
        ["kotlin"] = "kotlin", ["kt"] = "kotlin",
        ["swift"] = "swift",
        ["cpp"] = "cpp", ["c++"] = "cpp",
        ["c"] = "c",
        ["php"] = "php",
        ["sh"] = "bash", ["bash"] = "bash", ["shell"] = "bash",
        ["ps"] = "powershell", ["powershell"] = "powershell",
        ["sql"] = "sql",
        ["html"] = "html", ["css"] = "css",
        ["yaml"] = "yaml", ["yml"] = "yaml",
        ["json"] = "json", ["xml"] = "xml",
        ["dockerfile"] = "dockerfile"
    };

    private static List<string> Validate(AnswerGenerationDto dto)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Explanation))
        {
            issues.Add("explanation is empty");
        }

        if (string.IsNullOrWhiteSpace(dto.CodeSnippet))
        {
            issues.Add("code_snippet is empty");
        }
        else if (dto.CodeSnippet.Trim().Split('\n').Length > MaxCodeLines)
        {
            issues.Add("code_snippet exceeds max lines");
        }

        return issues;
    }

    public static string? RenderIfValid(AnswerGenerationDto dto)
    {
        var issues = Validate(dto);
        if (issues.Count > 0)
        {
            return null;
        }

        var html = Render(dto);
        return html.Length >= MinHtmlLength ? html : null;
    }

    private static string Render(AnswerGenerationDto dto)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(dto.Explanation))
        {
            sb.Append("<p>").Append(dto.Explanation.Trim()).Append("</p>");
        }

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
                .Append(WebUtility.HtmlEncode(dto.CodeSnippet.Trim()))
                .Append("</code></pre>");
        }

        if (!string.IsNullOrWhiteSpace(dto.Notes) && !IsPlaceholder(dto.Notes))
        {
            sb.Append("<h3>Notes</h3><p>").Append(dto.Notes.Trim()).Append("</p>");
        }

        return sb.ToString();
    }

    private static string NormaliseLanguage(string? raw)
    {
        var key = raw?.Trim().ToLowerInvariant();
        return key != null && LanguageAliases.TryGetValue(key, out var lang) ? lang : "";
    }

    private static readonly HashSet<string> KnownPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "empty string", "n/a", "none", "optional tip or empty string",
            "no notes", "no additional notes"
        };

    private static bool IsPlaceholder(string value) =>
        KnownPlaceholders.Contains(value.Trim());
}