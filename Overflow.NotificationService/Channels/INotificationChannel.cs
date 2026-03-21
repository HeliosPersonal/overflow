using Overflow.NotificationService.Templates;

namespace Overflow.NotificationService.Channels;

/// <summary>Notification delivery channel. <see cref="ChannelName" /> must match <c>NotificationChannel</c> enum.</summary>
public interface INotificationChannel
{
    string ChannelName { get; }

    Task SendAsync(string recipient, string subject, string body, string? plainTextBody = null,
        IReadOnlyList<InlineImage>? inlineImages = null);
}