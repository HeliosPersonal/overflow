namespace Overflow.EstimationService.DTOs;

public record CreateRoomRequest(
    string Title,
    string? DeckType = null,
    string? DisplayName = null,
    List<string>? Tasks = null);

public record JoinRoomRequest(string? DisplayName = null);

public record ChangeModeRequest(bool IsSpectator);

public record SubmitVoteRequest(string Value);

public record UpdateTasksRequest(List<string> Tasks);

public record RevoteRequest(int? RoundNumber = null);

public record RenameRoomRequest(string Title);