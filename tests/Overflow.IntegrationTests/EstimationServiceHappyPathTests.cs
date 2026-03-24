using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Models;
using Overflow.IntegrationTests.Fixtures;
using Shouldly;

namespace Overflow.IntegrationTests;

/// <summary>
/// EstimationService happy path: create room → join → vote → reveal → verify state.
/// No Wolverine/RabbitMQ — EstimationService uses EF Core + FusionCache only.
/// </summary>
public class EstimationServiceHappyPathTests : IClassFixture<EstimationServiceFixture>
{
    private readonly EstimationServiceFixture _factory;

    public EstimationServiceHappyPathTests(EstimationServiceFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullRoomLifecycle_CreateJoinVoteRevealReset()
    {
        // ── Setup ────────────────────────────────────────────────────────
        await _factory.EnsureDatabaseAsync();

        var moderator = _factory.CreateAuthenticatedClient("moderator-1");

        // ── 1. Create room ───────────────────────────────────────────────
        var createResponse = await moderator.PostAsJsonAsync("/estimation/rooms", new
        {
            title = "Sprint 42 Planning",
            deckType = "fibonacci",
            tasks = new[] { "Login page", "Dashboard", "API integration" }
        }, cancellationToken: TestContext.Current.CancellationToken);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        room.ShouldNotBeNull();
        room!.Title.ShouldBe("Sprint 42 Planning");
        room.Status.ShouldBe(RoomStatus.Voting);
        room.RoundNumber.ShouldBe(1);
        room.Deck.Id.ShouldBe("fibonacci");
        room.Tasks.ShouldNotBeNull();
        room.Tasks!.Count.ShouldBe(3);
        room.Participants.Count.ShouldBe(1);
        room.Viewer.IsModerator.ShouldBeTrue();
        room.Viewer.DisplayName.ShouldNotBeNullOrWhiteSpace();
        room.CurrentTaskName.ShouldBe("Login page");

        var roomId = room.RoomId;

        // ── 2. Get room — verify persistence ─────────────────────────────
        var getResponse =
            await moderator.GetAsync($"/estimation/rooms/{roomId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched =
            await getResponse.Content.ReadFromJsonAsync<RoomResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        fetched!.RoomId.ShouldBe(roomId);
        fetched.Title.ShouldBe("Sprint 42 Planning");

        // ── 3. Second participant joins ──────────────────────────────────
        var participant = _factory.CreateAuthenticatedClient("participant-1");
        var joinResponse = await participant.PostAsJsonAsync($"/estimation/rooms/{roomId}/join", new { },
            cancellationToken: TestContext.Current.CancellationToken);
        joinResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var joinedRoom =
            await joinResponse.Content.ReadFromJsonAsync<RoomResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        joinedRoom!.Participants.Count.ShouldBe(2);
        joinedRoom.Viewer.IsModerator.ShouldBeFalse();

        // ── 4. Both participants vote ────────────────────────────────────
        var modVoteResponse = await moderator.PostAsJsonAsync($"/estimation/rooms/{roomId}/votes", new { value = "5" },
            cancellationToken: TestContext.Current.CancellationToken);
        modVoteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var partVoteResponse = await participant.PostAsJsonAsync($"/estimation/rooms/{roomId}/votes",
            new { value = "8" }, cancellationToken: TestContext.Current.CancellationToken);
        partVoteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify participants show as "HasVoted" but votes are hidden
        var votingState =
            await partVoteResponse.Content.ReadFromJsonAsync<RoomResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        votingState!.Status.ShouldBe(RoomStatus.Voting);
        votingState.Participants.ShouldAllBe(p => p.HasVoted);
        // Votes should NOT be revealed yet
        votingState.Participants.ShouldAllBe(p => p.RevealedVote == null);

        // ── 5. Moderator reveals votes ───────────────────────────────────
        var revealResponse = await moderator.PostAsync($"/estimation/rooms/{roomId}/reveal", null,
            TestContext.Current.CancellationToken);
        revealResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var revealed =
            await revealResponse.Content.ReadFromJsonAsync<RoomResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        revealed!.Status.ShouldBe(RoomStatus.Revealed);
        revealed.RoundSummary.VotesRevealed.ShouldBeTrue();
        revealed.RoundSummary.ActiveVoterCount.ShouldBe(2);

        // Votes are now visible
        var modVote = revealed.Participants.First(p => p.ParticipantId == "moderator-1");
        modVote.RevealedVote.ShouldBe("5");
        var partVote = revealed.Participants.First(p => p.ParticipantId == "participant-1");
        partVote.RevealedVote.ShouldBe("8");

        // Distribution should show 5→1, 8→1
        revealed.RoundSummary.Distribution.ShouldNotBeNull();
        revealed.RoundSummary.Distribution!["5"].ShouldBe(1);
        revealed.RoundSummary.Distribution["8"].ShouldBe(1);

        // Numeric average of 5 and 8 = 6.5
        revealed.RoundSummary.NumericAverage.ShouldNotBeNull();
        revealed.RoundSummary.NumericAverage!.Value.ShouldBe(6.5, 0.01);

        // ── 6. Moderator resets round → advances to next task ────────────
        var resetResponse = await moderator.PostAsync($"/estimation/rooms/{roomId}/reset", null,
            TestContext.Current.CancellationToken);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterReset =
            await resetResponse.Content.ReadFromJsonAsync<RoomResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        afterReset!.Status.ShouldBe(RoomStatus.Voting);
        afterReset.RoundNumber.ShouldBe(2);
        afterReset.CurrentTaskName.ShouldBe("Dashboard"); // second task

        // Votes should be cleared
        afterReset.Participants.ShouldAllBe(p => !p.HasVoted);
        afterReset.Participants.ShouldAllBe(p => p.RevealedVote == null);

        // Round history should have 1 entry for round 1
        afterReset.RoundHistory.Count.ShouldBe(1);
        afterReset.RoundHistory[0].RoundNumber.ShouldBe(1);
        afterReset.RoundHistory[0].TaskName.ShouldBe("Login page");

        // ── 7. Verify DB state directly ──────────────────────────────────
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();

        var dbRoom = await db.Rooms
            .Include(r => r.Participants)
            .Include(r => r.Votes)
            .Include(r => r.RoundHistory)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken: TestContext.Current.CancellationToken);

        dbRoom.ShouldNotBeNull();
        dbRoom!.Status.ShouldBe(RoomStatus.Voting);
        dbRoom.RoundNumber.ShouldBe(2);
        dbRoom.Participants.Count.ShouldBe(2);
        dbRoom.Participants.ShouldContain(p => p.ParticipantId == "moderator-1" && p.IsModerator);
        dbRoom.Participants.ShouldContain(p => p.ParticipantId == "participant-1" && !p.IsModerator);

        // Round 1 votes are deleted on reset (historical data lives in RoundHistory)
        dbRoom.Votes.ShouldBeEmpty();

        // Round history should have 1 completed round
        dbRoom.RoundHistory.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetNonExistentRoom_Returns404()
    {
        await _factory.EnsureDatabaseAsync();
        var client = _factory.CreateAuthenticatedClient("user-1");

        var response =
            await client.GetAsync($"/estimation/rooms/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateRoom_EmptyTitle_ReturnsBadRequest()
    {
        await _factory.EnsureDatabaseAsync();
        var client = _factory.CreateAuthenticatedClient("user-1");

        var response = await client.PostAsJsonAsync("/estimation/rooms", new
        {
            title = "",
            deckType = "fibonacci"
        }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}