using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Commands;

// ─── Reveal Votes ────────────────────────────────────────────────────────

public record RevealVotesCommand(
    Guid RoomId,
    string UserId,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class RevealVotesHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<RevealVotesCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(RevealVotesCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.RevealVotesAsync(request.RoomId, request.UserId);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.UserId, request.BaseUrl, avatarLookup);
    }
}

// ─── Reset Round ─────────────────────────────────────────────────────────

public record ResetRoundCommand(
    Guid RoomId,
    string UserId,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class ResetRoundHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<ResetRoundCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(ResetRoundCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.ResetRoundAsync(request.RoomId, request.UserId);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.UserId, request.BaseUrl, avatarLookup);
    }
}

// ─── Revote ──────────────────────────────────────────────────────────────

public record RevoteCommand(
    Guid RoomId,
    string UserId,
    int? RoundNumber,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class RevoteHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<RevoteCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(RevoteCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.RevoteAsync(request.RoomId, request.UserId, request.RoundNumber);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.UserId, request.BaseUrl, avatarLookup);
    }
}

// ─── Update Tasks ────────────────────────────────────────────────────────

public record UpdateTasksCommand(
    Guid RoomId,
    string UserId,
    List<string> Tasks,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class UpdateTasksHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<UpdateTasksCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(UpdateTasksCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.UpdateTasksAsync(request.RoomId, request.UserId, request.Tasks);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.UserId, request.BaseUrl, avatarLookup);
    }
}

// ─── Rename Room ─────────────────────────────────────────────────────────

public record RenameRoomCommand(
    Guid RoomId,
    string UserId,
    string Title,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class RenameRoomHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<RenameRoomCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(RenameRoomCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.RenameRoomAsync(request.RoomId, request.UserId, request.Title);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.UserId, request.BaseUrl, avatarLookup);
    }
}

// ─── Archive Room ────────────────────────────────────────────────────────

public record ArchiveRoomCommand(
    Guid RoomId,
    string UserId,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class ArchiveRoomHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<ArchiveRoomCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(ArchiveRoomCommand request,
        CancellationToken cancellationToken)
    {
        var result = await svc.ArchiveRoomAsync(request.RoomId, request.UserId);
        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, request.UserId, request.BaseUrl, avatarLookup);
    }
}

// ─── Delete Room ─────────────────────────────────────────────────────────

public record DeleteRoomCommand(Guid RoomId, string UserId) : ICommand<UnitResult<RoomError>>;

public class DeleteRoomHandler(EstimationRoomService svc) : IRequestHandler<DeleteRoomCommand, UnitResult<RoomError>>
{
    public async Task<UnitResult<RoomError>> Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        return await svc.DeleteRoomAsync(request.RoomId, request.UserId);
    }
}