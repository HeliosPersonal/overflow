namespace Overflow.NotificationService.Templates;

/// <summary>Inline email image referenced as <c>cid:{FileName}</c> in HTML body.</summary>
public record InlineImage(string ContentId, byte[] Data, string ContentType, string FileName);