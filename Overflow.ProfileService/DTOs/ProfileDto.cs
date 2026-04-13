using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.DTOs;

public record ProfileDto(
    string UserId,
    string DisplayName,
    string? Email,
    string? Description,
    string? AvatarUrl,
    int Reputation,
    DateTime JoinedAt,
    ThemePreference ThemePreference);