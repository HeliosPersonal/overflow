using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Overflow.EstimationService.Events;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Projections;

/// <summary>
/// Inline projection that builds <see cref="EstimationRoomView"/> from the room's event stream.
/// Uses Marten's EventProjection to store/update a document per room.
/// </summary>
public class EstimationRoomProjection : EventProjection
{
    public EstimationRoomProjection()
    {
        ProjectAsync<IEvent<RoomCreated>>(ApplyRoomCreated);
        ProjectAsync<IEvent<ParticipantJoined>>(ApplyParticipantJoined);
        ProjectAsync<IEvent<ParticipantModeChanged>>(ApplyParticipantModeChanged);
        ProjectAsync<IEvent<ParticipantLeft>>(ApplyParticipantLeft);
        ProjectAsync<IEvent<VoteSubmitted>>(ApplyVoteSubmitted);
        ProjectAsync<IEvent<VoteCleared>>(ApplyVoteCleared);
        ProjectAsync<IEvent<VotesRevealed>>(ApplyVotesRevealed);
        ProjectAsync<IEvent<RoundReset>>(ApplyRoundReset);
        ProjectAsync<IEvent<RoomArchived>>(ApplyRoomArchived);
    }

    private static Task ApplyRoomCreated(IEvent<RoomCreated> ev, IDocumentOperations ops, CancellationToken ct)
    {
        var e = ev.Data;
        var view = new EstimationRoomView
        {
            Id = e.RoomId,
            Code = e.Code,
            Title = e.Title,
            ModeratorUserId = e.ModeratorUserId,
            DeckType = e.DeckType,
            DeckValues = e.DeckValues,
            Status = RoomStatus.Voting,
            RoundNumber = 1,
            CreatedAtUtc = e.CreatedAtUtc,
            UpdatedAtUtc = e.CreatedAtUtc,
            Participants =
            [
                new ParticipantView
                {
                    ParticipantId = e.ModeratorUserId,
                    UserId = e.ModeratorUserId,
                    DisplayName = e.ModeratorDisplayName,
                    IsGuest = false,
                    IsModerator = true,
                    IsSpectator = false,
                    JoinedAtUtc = e.CreatedAtUtc,
                    LastSeenAtUtc = e.CreatedAtUtc
                }
            ],
            Votes = []
        };
        ops.Store(view);
        return Task.CompletedTask;
    }

    private static async Task ApplyParticipantJoined(IEvent<ParticipantJoined> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var e = ev.Data;
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        var existing = view.Participants.FirstOrDefault(p => p.ParticipantId == e.ParticipantId);
        if (existing is not null)
        {
            existing.LastSeenAtUtc = e.JoinedAtUtc;
            existing.LeftAtUtc = null;
        }
        else
        {
            view.Participants.Add(new ParticipantView
            {
                ParticipantId = e.ParticipantId,
                UserId = e.UserId,
                GuestId = e.GuestId,
                DisplayName = e.DisplayName,
                IsGuest = e.IsGuest,
                IsModerator = false,
                IsSpectator = false,
                JoinedAtUtc = e.JoinedAtUtc,
                LastSeenAtUtc = e.JoinedAtUtc
            });
        }

        view.UpdatedAtUtc = e.JoinedAtUtc;
        ops.Store(view);
    }

    private static async Task ApplyParticipantModeChanged(IEvent<ParticipantModeChanged> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var e = ev.Data;
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        var participant = view.Participants.FirstOrDefault(p => p.ParticipantId == e.ParticipantId);
        if (participant is null) return;

        participant.IsSpectator = e.IsSpectator;
        view.UpdatedAtUtc = e.ChangedAtUtc;

        if (e.IsSpectator && view.Status == RoomStatus.Voting)
        {
            view.Votes.RemoveAll(v =>
                v.ParticipantId == e.ParticipantId && v.RoundNumber == view.RoundNumber);
        }

        ops.Store(view);
    }

    private static async Task ApplyParticipantLeft(IEvent<ParticipantLeft> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var e = ev.Data;
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        var participant = view.Participants.FirstOrDefault(p => p.ParticipantId == e.ParticipantId);
        if (participant is null) return;

        participant.LeftAtUtc = e.LeftAtUtc;
        view.UpdatedAtUtc = e.LeftAtUtc;
        ops.Store(view);
    }

    private static async Task ApplyVoteSubmitted(IEvent<VoteSubmitted> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var e = ev.Data;
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        view.Votes.RemoveAll(v =>
            v.ParticipantId == e.ParticipantId && v.RoundNumber == e.RoundNumber);

        view.Votes.Add(new RoundVoteView
        {
            RoundNumber = e.RoundNumber,
            ParticipantId = e.ParticipantId,
            Value = e.Value,
            SubmittedAtUtc = e.SubmittedAtUtc
        });
        view.UpdatedAtUtc = e.SubmittedAtUtc;
        ops.Store(view);
    }

    private static async Task ApplyVoteCleared(IEvent<VoteCleared> ev, IDocumentOperations ops, CancellationToken ct)
    {
        var e = ev.Data;
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        view.Votes.RemoveAll(v =>
            v.ParticipantId == e.ParticipantId && v.RoundNumber == e.RoundNumber);
        view.UpdatedAtUtc = e.ClearedAtUtc;
        ops.Store(view);
    }

    private static async Task ApplyVotesRevealed(IEvent<VotesRevealed> ev, IDocumentOperations ops,
        CancellationToken ct)
    {
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        view.Status = RoomStatus.Revealed;
        view.UpdatedAtUtc = ev.Data.RevealedAtUtc;
        ops.Store(view);
    }

    private static async Task ApplyRoundReset(IEvent<RoundReset> ev, IDocumentOperations ops, CancellationToken ct)
    {
        var e = ev.Data;
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        view.RoundNumber = e.NewRoundNumber;
        view.Status = RoomStatus.Voting;
        view.Votes.RemoveAll(v => v.RoundNumber != e.NewRoundNumber);
        view.UpdatedAtUtc = e.ResetAtUtc;
        ops.Store(view);
    }

    private static async Task ApplyRoomArchived(IEvent<RoomArchived> ev, IDocumentOperations ops, CancellationToken ct)
    {
        var view = await ops.LoadAsync<EstimationRoomView>(ev.StreamId, ct);
        if (view is null) return;

        view.Status = RoomStatus.Archived;
        view.ArchivedAtUtc = ev.Data.ArchivedAtUtc;
        view.UpdatedAtUtc = ev.Data.ArchivedAtUtc;
        ops.Store(view);
    }
}