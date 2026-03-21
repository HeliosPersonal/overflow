using System.Reflection;
using System.Text.RegularExpressions;
using Overflow.Contracts;

namespace Overflow.NotificationService.Templates;

/// <summary>
/// Renders templates from embedded HTML/text files. Placeholders use {{key}} syntax.
/// To add a template: add enum → create Html/Text files → add subject in <see cref="Subjects"/>.
/// </summary>
public partial class TemplateRenderer : ITemplateRenderer
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    private const string LogoContentId = "overflow-logo";
    private static readonly Lazy<InlineImage?> LogoImage = new(LoadLogoImage);

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

        var allParams = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
        {
            ["year"] = DateTime.UtcNow.Year.ToString()
        };

        var subject = ReplacePlaceholders(subjectTemplate, allParams);
        var html = ReplacePlaceholders(htmlRaw, allParams);
        var text = ReplacePlaceholders(textRaw, allParams);

        var inlineImages = LogoImage.Value is { } logo ? [logo] : new List<InlineImage>();

        return new RenderedTemplate(subject, html, text, inlineImages);
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
        var resourceName = $"Overflow.NotificationService.{relativePath}";
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static InlineImage? LoadLogoImage()
    {
        const string resourceName = "Overflow.NotificationService.Templates.Images.overflow-logo.png";
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return new InlineImage(LogoContentId, ms.ToArray(), "image/png", "overflow-logo.png");
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderRegex();
}