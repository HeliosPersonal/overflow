using FluentEmail.Core;
using FluentEmail.Core.Models;
using Overflow.Contracts;
using Overflow.NotificationService.Templates;

namespace Overflow.NotificationService.Channels;

/// <summary>Email channel via FluentEmail + Mailgun. Supports CID inline images.</summary>
public class MailgunEmailChannel(IFluentEmail fluentEmail, ILogger<MailgunEmailChannel> logger) : INotificationChannel
{
    public string ChannelName => nameof(NotificationChannel.Email);

    public async Task SendAsync(string recipient, string subject, string body, string? plainTextBody = null,
        IReadOnlyList<InlineImage>? inlineImages = null)
    {
        var email = fluentEmail
            .To(recipient)
            .Subject(subject)
            .Body(body, isHtml: true);

        if (!string.IsNullOrEmpty(plainTextBody))
        {
            email.PlaintextAlternativeBody(plainTextBody);
        }

        // Mailgun uses FILENAME as CID (not ContentId) — templates reference cid:{filename}
        if (inlineImages is { Count: > 0 })
        {
            foreach (var image in inlineImages)
            {
                var attachment = new Attachment
                {
                    Data = new MemoryStream(image.Data),
                    Filename = image.FileName,
                    ContentType = image.ContentType,
                    ContentId = image.ContentId,
                    IsInline = true
                };
                email.Attach(attachment);
            }
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