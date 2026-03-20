using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Commands;

public record JoinRoomCommand(
    Guid RoomId,
    string? RequestDisplayName,
    IdentityResolver.ParticipantIdentity Identity,
    string BaseUrl,
    string? AccessToken) : ICommand<Result<RoomResponse, RoomError>>;

public class JoinRoomHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<JoinRoomCommand, Result<RoomResponse, RoomError>>
{
    public async Task<Result<RoomResponse, RoomError>> Handle(JoinRoomCommand request,
        CancellationToken cancellationToken)
    {
        var identity = request.Identity;

        // Guest without display name — check if already a participant
        if (identity.IsGuest && string.IsNullOrWhiteSpace(request.RequestDisplayName))
        {
            var existingRoom = await svc.GetRoomByIdAsync(request.RoomId);
            var existingParticipant = existingRoom?.Participants
                .FirstOrDefault(p => p.ParticipantId == identity.ParticipantId);

            if (existingParticipant is null)
                return RoomErrors.InvalidState("Display name is required for guest participants");

            identity = identity with { DisplayName = existingParticipant.DisplayName };
        }

        var result = await svc.JoinRoomAsync(
            request.RoomId, identity.ParticipantId, identity.UserId,
            identity.GuestId, identity.DisplayName, identity.IsGuest);

        if (result.IsFailure) return result.Error;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(result.Value, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(result.Value, identity.ParticipantId, request.BaseUrl, avatarLookup);
    }
}