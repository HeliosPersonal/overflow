namespace Overflow.EstimationService.DTOs;

public record CreateRoomRequest(string Title, string? DeckType = null, string? DisplayName = null);

public record JoinRoomRequest(string? DisplayName = null);

public record ChangeModeRequest(bool IsSpectator);

public record SubmitVoteRequest(string Value);