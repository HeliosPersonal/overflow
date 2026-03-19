using Overflow.Contracts;

namespace Overflow.NotificationService.Templates;

/// <summary>
/// Rendered notification content returned by <see cref="ITemplateRenderer"/>.
/// </summary>
public record RenderedTemplate(string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Resolves a template by enum and renders it with the given parameters.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Render a template. Returns null if the template is unknown.
    /// </summary>
    RenderedTemplate? Render(NotificationTemplate template, Dictionary<string, string> parameters);
}