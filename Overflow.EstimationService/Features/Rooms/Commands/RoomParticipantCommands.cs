using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Commands;

// ─── Change Mode ─────────────────────────────────────────────────────────

public record ChangeModeCommand(
    Guid RoomId,
    bool IsSpectator,
    string ParticipantId,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class ChangeModeHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<ChangeModeCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(ChangeModeCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.ChangeModeAsync(request.RoomId, request.ParticipantId, request.IsSpectator);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.ParticipantId, request.BaseUrl, avatarLookup);
    }
}

// ─── Submit Vote ─────────────────────────────────────────────────────────

public record SubmitVoteCommand(
    Guid RoomId,
    string Value,
    string ParticipantId,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class SubmitVoteHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<SubmitVoteCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(SubmitVoteCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.SubmitVoteAsync(request.RoomId, request.ParticipantId, request.Value);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.ParticipantId, request.BaseUrl, avatarLookup);
    }
}

// ─── Clear Vote ──────────────────────────────────────────────────────────

public record ClearVoteCommand(Guid RoomId, string ParticipantId) : ICommand<UnitResult<RoomError>>;

public class ClearVoteHandler(EstimationRoomService svc) : IRequestHandler<ClearVoteCommand, UnitResult<RoomError>>
{
    public async Task<UnitResult<RoomError>> Handle(ClearVoteCommand request, CancellationToken cancellationToken)
    {
        return await svc.ClearVoteAsync(request.RoomId, request.ParticipantId);
    }
}

// ─── Leave Room ──────────────────────────────────────────────────────────

public record LeaveRoomCommand(Guid RoomId, string ParticipantId) : ICommand<UnitResult<RoomError>>;

public class LeaveRoomHandler(EstimationRoomService svc) : IRequestHandler<LeaveRoomCommand, UnitResult<RoomError>>
{
    public async Task<UnitResult<RoomError>> Handle(LeaveRoomCommand request, CancellationToken cancellationToken)
    {
        return await svc.LeaveRoomAsync(request.RoomId, request.ParticipantId);
    }
}