using CommandFlow;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Queries;

// ─── Get Room ────────────────────────────────────────────────────────────

public record GetRoomQuery(
    Guid RoomId,
    string ViewerParticipantId,
    string BaseUrl,
    string? AccessToken) : IQuery<RoomResponse?>;

public class GetRoomHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<GetRoomQuery, RoomResponse?>
{
    public async Task<RoomResponse?> Handle(GetRoomQuery request, CancellationToken cancellationToken)
    {
        var room = await svc.GetRoomByIdAsync(request.RoomId);
        if (room is null) return null;

        var avatarLookup = await AvatarResolver.ResolveForRoomAsync(room, request.AccessToken, profileClient);
        return RoomResponseMapper.ToResponse(room, request.ViewerParticipantId, request.BaseUrl, avatarLookup);
    }
}

// ─── Get My Rooms ────────────────────────────────────────────────────────

public record GetMyRoomsQuery(
    string UserId,
    int RetentionDays,
    string? AccessToken) : IQuery<List<RoomSummaryResponse>>;

public class GetMyRoomsHandler(
    EstimationRoomService svc,
    ProfileServiceClient profileClient) : IRequestHandler<GetMyRoomsQuery, List<RoomSummaryResponse>>
{
    public async Task<List<RoomSummaryResponse>> Handle(GetMyRoomsQuery request, CancellationToken cancellationToken)
    {
        var rooms = await svc.GetRoomsForUserAsync(request.UserId);

        var allUserIds = rooms
            .SelectMany(r => r.Participants)
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!)
            .Distinct()
            .ToList();

        var avatarLookup = await AvatarResolver.ResolveAsync(allUserIds, request.AccessToken, profileClient);

        return rooms
            .OrderBy(r => r.Status)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Select(r =>
            {
                var creator = r.Participants.FirstOrDefault(p => p.IsModerator)
                              ?? r.Participants.FirstOrDefault();
                return new RoomSummaryResponse(
                    r.Id,
                    r.Title,
                    r.Status,
                    r.RoundNumber,
                    r.Participants.Count,
                    r.RoundHistory.Count,
                    r.CreatedAtUtc,
                    r.ArchivedAtUtc,
                    r.ModeratorUserId == request.UserId,
                    request.RetentionDays,
                    creator?.DisplayName ?? "Unknown",
                    creator?.UserId is not null ? avatarLookup.GetValueOrDefault(creator.UserId) : null,
                    r.Participants
                        .Select(p => new ParticipantSummaryResponse(
                            p.DisplayName,
                            p.UserId is not null ? avatarLookup.GetValueOrDefault(p.UserId) : null))
                        .ToList()
                );
            }).ToList();
    }
}