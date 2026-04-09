using System.Net;
using System.Text;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public static class AnswerHtmlRenderer
{
    private const int MinHtmlLength = 50;
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

        if (!string.IsNullOrWhiteSpace(dto.CodeSnippet) &&
            dto.CodeSnippet.Trim().Split('\n').Length > MaxCodeLines)
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

        var points = dto.Points.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (points.Count > 0)
        {
            sb.Append("<ul>");
            foreach (var point in points)
                sb.Append("<li>").Append(point.Trim()).Append("</li>");
            sb.Append("</ul>");
        }

        if (!string.IsNullOrWhiteSpace(dto.CodeSnippet))
        {
            var lang = NormaliseLanguage(dto.Language);
            sb.Append($"<pre><code class=\"language-{lang}\">")
                .Append(WebUtility.HtmlEncode(dto.CodeSnippet.Trim()))
                .Append("</code></pre>");
        }


        return sb.ToString();
    }

    private static string NormaliseLanguage(string? raw)
    {
        var key = raw?.Trim().ToLowerInvariant();
        return key != null && LanguageAliases.TryGetValue(key, out var lang) ? lang : "";
    }
}