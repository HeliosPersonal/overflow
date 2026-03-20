using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Commands;

// ─── Create Room ────────────────────────────────────────────────────────────

public record CreateRoomCommand(
    string Title,
    string? DeckType,
    List<string>? Tasks,
    IdentityResolver.ParticipantIdentity Identity,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class CreateRoomHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<CreateRoomCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(CreateRoomCommand request,
        CancellationToken cancellationToken)
    {
        var id = request.Identity;
        var result = await svc.CreateRoomAsync(
            request.Title, id.ParticipantId, id.UserId, id.GuestId,
            id.DisplayName, id.IsGuest, request.DeckType, request.Tasks);

        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, id.ParticipantId, request.BaseUrl, avatarLookup);
    }
}