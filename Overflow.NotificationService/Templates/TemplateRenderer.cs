using System.Reflection;
using System.Text.RegularExpressions;
using Overflow.Contracts;

namespace Overflow.NotificationService.Templates;

/// <summary>
/// Renders notification templates from embedded HTML/text resource files.
///
/// Template files live in Templates/Html/{TemplateName}.html and Templates/Text/{TemplateName}.txt.
/// Placeholders use the {{key}} syntax and are replaced with values from the parameters dictionary.
/// The built-in {{year}} placeholder is always available.
///
/// To add a new template:
///   1. Add the enum value to <see cref="NotificationTemplate"/>
///   2. Create Html/{EnumName}.html and Text/{EnumName}.txt
///   3. Add a subject mapping in <see cref="Subjects"/>
/// </summary>
public partial class TemplateRenderer : ITemplateRenderer
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    /// <summary>
    /// Subject line per template. Supports {{key}} placeholders.
    /// </summary>
    private static readonly Dictionary<NotificationTemplate, string> Subjects = new()
    {
        [NotificationTemplate.PasswordReset] = "Reset your {{appName}} password",
        [NotificationTemplate.Welcome] = "Welcome to {{appName}}!",
        [NotificationTemplate.VerifyEmail] = "Verify your {{appName}} email address",
    };

    public RenderedTemplate? Render(NotificationTemplate template, Dictionary<string, string> parameters)
    {
        if (!Subjects.TryGetValue(template, out var subjectTemplate))
            return null;

        var htmlRaw = LoadEmbeddedResource($"Templates.Html.{template}.html");
        var textRaw = LoadEmbeddedResource($"Templates.Text.{template}.txt");

        if (htmlRaw is null || textRaw is null)
            return null;

        // Add built-in placeholders
        var allParams = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
        {
            ["year"] = DateTime.UtcNow.Year.ToString()
        };

        var subject = ReplacePlaceholders(subjectTemplate, allParams);
        var html = ReplacePlaceholders(htmlRaw, allParams);
        var text = ReplacePlaceholders(textRaw, allParams);

        return new RenderedTemplate(subject, html, text);
    }

    private static string ReplacePlaceholders(string content, Dictionary<string, string> parameters)
    {
        return PlaceholderRegex().Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            return parameters.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static string? LoadEmbeddedResource(string relativePath)
    {
        // Embedded resource names use the default namespace + folder path with dots
        var resourceName = $"Overflow.NotificationService.{relativePath}";
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderRegex();
}