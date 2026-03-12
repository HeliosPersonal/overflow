namespace Overflow.EstimationService.Exceptions;

/// <summary>
/// Domain error with a code (for HTTP status mapping) and user-facing message.
/// </summary>
public record RoomError(RoomErrorCode Code, string Message);

public enum RoomErrorCode
{
    NotFound,
    Archived,
    ParticipantNotFound,
    Forbidden,
    InvalidState,
    InvalidVote
}

public static class RoomErrors
{
    public static RoomError NotFound(Guid roomId) => new(RoomErrorCode.NotFound, $"Room not found: {roomId}");
    public static RoomError Archived(Guid roomId) => new(RoomErrorCode.Archived, $"Room is archived: {roomId}");

    public static RoomError ParticipantNotFound(string id) =>
        new(RoomErrorCode.ParticipantNotFound, $"Not a participant: {id}");

    public static RoomError NotModerator() =>
        new(RoomErrorCode.Forbidden, "Only the moderator can perform this action");

    public static RoomError SpectatorCannotVote() => new(RoomErrorCode.InvalidState, "Spectators cannot vote");
    public static RoomError InvalidState(string msg) => new(RoomErrorCode.InvalidState, msg);
    public static RoomError InvalidVote(string value) => new(RoomErrorCode.InvalidVote, $"Invalid vote value: {value}");
}