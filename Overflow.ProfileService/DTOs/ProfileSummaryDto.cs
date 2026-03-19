namespace Overflow.ProfileService.DTOs;

public record ProfileSummaryDto(string UserId, string DisplayName, int Reputation, string? AvatarUrl);