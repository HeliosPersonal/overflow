namespace Overflow.ProfileService.DTOs;

public record ProfileDto(
    string UserId,
    string DisplayName,
    string? Description,
    string? AvatarUrl,
    int Reputation,
    DateTime JoinedAt);