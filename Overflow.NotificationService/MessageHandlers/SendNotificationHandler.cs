using Overflow.Contracts;
using Overflow.NotificationService.Channels;
using Overflow.NotificationService.Templates;

namespace Overflow.NotificationService.MessageHandlers;

/// <summary>Consumes <see cref="SendNotification"/> from RabbitMQ, renders the template, and dispatches to the channel.</summary>
public class SendNotificationHandler(
    ITemplateRenderer templateRenderer,
    IEnumerable<INotificationChannel> channels,
    ILogger<SendNotificationHandler> logger)
{
    private readonly Dictionary<string, INotificationChannel> _channels =
        channels.ToDictionary(c => c.ChannelName, c => c, StringComparer.OrdinalIgnoreCase);

    public async Task HandleAsync(SendNotification message)
    {
        logger.LogInformation(
            "Processing notification: channel={Channel}, template={Template}, to={Recipient}",
            message.Channel, message.Template, message.Recipient);

        var rendered = templateRenderer.Render(message.Template, message.Parameters);
        if (rendered is null)
        {
            logger.LogError("Unknown or missing template: {Template}", message.Template);
            return;
        }

        var channelName = message.Channel.ToString();
        if (!_channels.TryGetValue(channelName, out var channel))
        {
            logger.LogError("Unsupported notification channel: {Channel}", message.Channel);
            return;
        }

        await channel.SendAsync(message.Recipient, rendered.Subject, rendered.HtmlBody, rendered.TextBody,
            rendered.InlineImages);
    }
}