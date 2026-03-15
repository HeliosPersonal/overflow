namespace Overflow.Contracts;

/// <summary>
/// Request to send a notification. Published to RabbitMQ and consumed by NotificationService.
/// The service resolves the template and dispatches to the appropriate channel.
/// </summary>
/// <param name="Channel">Delivery channel</param>
/// <param name="Recipient">Destination address (email address, Telegram chat ID, etc.)</param>
/// <param name="Template">Template to render</param>
/// <param name="Parameters">Key-value pairs injected into the template (e.g. resetUrl, appName)</param>
public record SendNotification(
    NotificationChannel Channel,
    string Recipient,
    NotificationTemplate Template,
    Dictionary<string, string> Parameters);