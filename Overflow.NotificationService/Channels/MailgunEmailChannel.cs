using FluentEmail.Core;
using Overflow.Contracts;

namespace Overflow.NotificationService.Channels;

/// <summary>
/// Email channel — sends via FluentEmail with the Mailgun sender.
/// </summary>
public class MailgunEmailChannel(IFluentEmail fluentEmail, ILogger<MailgunEmailChannel> logger) : INotificationChannel
{
    public string ChannelName => nameof(NotificationChannel.Email);

    public async Task SendAsync(string recipient, string subject, string body, string? plainTextBody = null)
    {
        var email = fluentEmail
            .To(recipient)
            .Subject(subject)
            .Body(body, isHtml: true);

        if (!string.IsNullOrEmpty(plainTextBody))
        {
            email.PlaintextAlternativeBody(plainTextBody);
        }

        var response = await email.SendAsync();

        if (!response.Successful)
        {
            var errors = string.Join("; ", response.ErrorMessages);
            logger.LogError("Failed to send email to {Recipient}: {Errors}", recipient, errors);
            throw new InvalidOperationException($"Email send failed: {errors}");
        }

        logger.LogInformation("Email sent to {Recipient}: \"{Subject}\"", recipient, subject);
    }
}