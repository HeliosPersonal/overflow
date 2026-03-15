namespace Overflow.Contracts;

/// <summary>
/// Available notification templates. Each maps to an HTML file
/// in NotificationService/Templates/Html/{name}.html
/// </summary>
public enum NotificationTemplate
{
    PasswordReset,
    Welcome,
    VerifyEmail,
}