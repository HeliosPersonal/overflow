using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.DTOs;

public record EditProfileDto(
    string? DisplayName,
    string? Description,
    string? AvatarUrl,
    ThemePreference? ThemePreference);