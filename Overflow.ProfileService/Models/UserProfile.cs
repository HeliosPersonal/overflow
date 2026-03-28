using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Overflow.ProfileService.Models;

public class UserProfile
{
    [MaxLength(36)] public string Id { get; set; } = Guid.NewGuid().ToString();
    [MaxLength(200)] public required string DisplayName { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    [MaxLength(2000)] public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public int Reputation { get; set; }

    [Column(TypeName = "varchar(10)")] public ThemePreference ThemePreference { get; set; } = ThemePreference.System;
}