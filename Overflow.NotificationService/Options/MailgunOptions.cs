namespace Overflow.NotificationService.Options;

/// <summary>
/// Mailgun configuration. Bound from appsettings "Mailgun" section.
/// Used to configure FluentEmail.Mailgun sender.
/// </summary>
public class MailgunOptions
{
    public const string SectionName = "Mailgun";

    /// <summary>Mailgun API key.</summary>
    public required string ApiKey { get; set; }

    /// <summary>Sending domain, e.g. "devoverflow.org".</summary>
    public required string Domain { get; set; }

    /// <summary>Default sender email, e.g. "noreply@devoverflow.org".</summary>
    public required string FromEmail { get; set; }

    /// <summary>Default sender display name, e.g. "Overflow".</summary>
    public required string FromName { get; set; }

    /// <summary>Mailgun region. "EU" or "US". Defaults to EU.</summary>
    public required string Region { get; set; }
}