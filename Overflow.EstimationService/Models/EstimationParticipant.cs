using System.Text.Json.Serialization;

namespace Overflow.EstimationService.Models;

public class EstimationParticipant
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string ParticipantId { get; set; } = string.Empty; // userId or "guest_{guid}"
    public string? UserId { get; set; }
    public string? GuestId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
    public bool IsModerator { get; set; }
    public bool IsSpectator { get; set; }
    public bool IsPresent { get; set; } = true;
    public DateTime JoinedAtUtc { get; set; }

    [JsonIgnore] public EstimationRoom Room { get; set; } = null!;
}