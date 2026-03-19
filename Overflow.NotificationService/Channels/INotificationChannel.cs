namespace Overflow.NotificationService.Channels;

/// <summary>
/// Abstraction for a notification delivery channel (email, Telegram, SMS, etc.).
/// <see cref="ChannelName"/> must match the corresponding <c>NotificationChannel</c> enum value
/// (e.g. "Email", "Telegram").
/// </summary>
public interface INotificationChannel
{
    /// <summary>Channel identifier — must match <c>NotificationChannel.ToString()</c> (e.g. "Email", "Telegram").</summary>
    string ChannelName { get; }

    /// <summary>Deliver a rendered notification to the recipient.</summary>
    Task SendAsync(string recipient, string subject, string body, string? plainTextBody = null);
}