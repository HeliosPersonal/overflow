using Overflow.Contracts;

namespace Overflow.NotificationService.Templates;

/// <summary>Rendered notification content (HTML + plaintext + inline images).</summary>
public record RenderedTemplate(
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<InlineImage> InlineImages);

/// <summary>Renders notification templates from embedded resources.</summary>
public interface ITemplateRenderer
{
    /// <summary>Render a template. Returns null if unknown.</summary>
    RenderedTemplate? Render(NotificationTemplate template, Dictionary<string, string> parameters);
}