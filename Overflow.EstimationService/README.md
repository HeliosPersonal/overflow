# EstimationService (Planning Poker)

Real-time Planning Poker estimation rooms with WebSocket push.

---

## Overview

|               |                                                                          |
|---------------|--------------------------------------------------------------------------|
| **Type**      | .NET 10 ASP.NET Core (Controllers + WebSocket endpoint)                  |
| **Database**  | PostgreSQL via EF Core (`estimationDb`)                                  |
| **Cache**     | FusionCache (L1 in-memory + L2 Redis) with Redis backplane               |
| **Real-time** | Raw WebSocket (server → client snapshots) + Redis pub/sub cross-pod sync |
| **Auth**      | Keycloak JWT — guests get real Keycloak accounts automatically           |
| **Port**      | 8080                                                                     |
| **Messaging** | None — no Wolverine or RabbitMQ dependency                               |
| **Scaling**   | Multi-pod safe via Redis-backed distributed cache + cross-pod broadcast  |

---

## Key Features

- **Room creation** — both authenticated users and guests can create rooms with a selected card deck
- **Guest access** — anonymous users provide a display name; the webapp creates a real Keycloak account automatically
  so they participate as fully authenticated users (see "Guest Auth" in `AGENTS.md`)
- **Legacy guest cookie** — `overflow_guest_id` cookie (30-day, HttpOnly) is still supported for backwards compatibility
- **Display name sync** — re-joining a room updates the participant's display name if it changed (e.g. after account
  upgrade)
- **Real-time updates** — WebSocket pushes personalized room snapshots on every state change
- **HTTP-only mutations** — vote, reveal, reset, archive all go through REST endpoints; WebSocket is read-only push
- **Disconnect = leave** — WebSocket close removes the participant from the room and broadcasts updated state
- **Guest-to-account claim** — `POST /estimation/claim-guest` migrates legacy cookie-based guest participation to an
  authenticated user
- **Automatic room cleanup** — archived rooms are permanently deleted after a configurable retention period (default: 30
  days)

---

## Archived Room Cleanup

Archived rooms are **automatically deleted after 30 days** (configurable) to keep storage lean.

| Setting                     | Default | Description                                              |
|-----------------------------|---------|----------------------------------------------------------|
| `RoomCleanup:RetentionDays` | `30`    | Days after archival before a room is permanently deleted |
| `RoomCleanup:IntervalHours` | `24`    | How often the cleanup background job runs                |

Implemented via `ArchivedRoomCleanupService` (`BackgroundService`). On each run it bulk-deletes expired rooms
along with all related votes, round history, and participants. Override via `appsettings.json`, environment
variables, or Infisical (`ROOM_CLEANUP__RETENTION_DAYS`, `ROOM_CLEANUP__INTERVAL_HOURS`).

---

## Caching & Multi-Pod Architecture

The service uses **FusionCache** with Redis as L2 distributed cache and backplane, plus Redis pub/sub for cross-pod
WebSocket broadcast. This enables horizontal scaling with multiple pods.

### Cache Layers

| Layer         | Technology          | Scope      | TTL                                          | Purpose                                         |
|---------------|---------------------|------------|----------------------------------------------|-------------------------------------------------|
| **L1**        | In-memory (per-pod) | Single pod | 30s (rooms), 60s (profiles), 2m (user rooms) | Sub-millisecond reads, zero network hops        |
| **L2**        | Redis (shared)      | All pods   | Same as L1                                   | Shared cache across pods, survives pod restarts |
| **Backplane** | Redis pub/sub       | All pods   | —                                            | L1 cache invalidation propagation across pods   |

### Multi-Pod WebSocket Broadcast

WebSocket connections are local to each pod (in-memory `ConcurrentDictionary`). When a mutation happens on pod A,
all pods must push updates to their local WebSocket connections:

```
Pod A (mutation)                          Pod B (WebSocket connections)
┌─────────────────────┐                   ┌─────────────────────────────┐
│ 1. EF Core → DB     │                   │                             │
│ 2. Cache invalidate │──── Redis ────────│ FusionCache backplane       │
│    (L1 + backplane) │    backplane      │ → evicts local L1 entry     │
│ 3. Publish roomId   │──── Redis ────────│ CrossPodBroadcastService    │
│    to pub/sub       │    pub/sub        │ → receives roomId           │
│ 4. Local broadcast  │                   │ → WebSocketBroadcaster      │
│    (Pod A sockets)  │                   │   loads room (cache/DB)     │
└─────────────────────┘                   │   broadcasts to local WS    │
                                          └─────────────────────────────┘
```

### Key Services

| Service                      | Lifetime           | Purpose                                                                                       |
|------------------------------|--------------------|-----------------------------------------------------------------------------------------------|
| `RoomCacheService`           | Singleton          | Cache-aside reads for rooms + user room lists via FusionCache                                 |
| `CrossPodBroadcastService`   | Singleton (hosted) | Redis pub/sub: publishes room mutations, subscribes on startup to trigger local WS broadcasts |
| `WebSocketBroadcaster`       | Singleton          | Tracks local WebSocket connections, sends viewer-scoped snapshots                             |
| `ArchivedRoomCleanupService` | Singleton (hosted) | Periodically deletes archived rooms past the configured retention period (default: 30 days)   |

### Fail-Safe Behavior

FusionCache provides automatic **fail-safe**: if the database is temporarily unreachable, stale cached data is served
(up to 5 minutes). If Redis is down, the service degrades gracefully to L1-only (in-memory) caching — it never blocks
on Redis unavailability.

---

## Workflow — Sequence Diagrams

### 1. Room Creation

A moderator (authenticated user or guest with auto-created Keycloak account) creates a new room.

```mermaid
sequenceDiagram
    actor User as Client (Browser)
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant IR as IdentityResolver
    participant PS as ProfileService
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL

    User->>GW: POST /estimation/rooms<br/>{ title, deckType?, displayName? }
    GW->>EC: Route to EstimationService
    EC->>IR: ResolveAsync(httpContext, displayName)

    alt Authenticated (JWT present)
        IR->>PS: GET /profiles/{userId}<br/>(with Bearer token)
        PS-->>IR: { displayName }
        IR-->>EC: ParticipantIdentity(userId, displayName, isGuest=false)
    else Legacy Guest (cookie only)
        IR-->>EC: ParticipantIdentity(guestId, displayName, isGuest=true)
    end

    EC->>SVC: CreateRoomAsync(title, participantId, ...)
    SVC->>DB: INSERT Room + Participant (moderator)
    DB-->>SVC: Room entity
    SVC->>SVC: Invalidate user rooms cache (FusionCache)
    SVC-->>EC: Result<Room>
    EC-->>GW: 201 Created + RoomResponse
    GW-->>User: RoomResponse { roomId, canonicalUrl, ... }
```

### 2. Join Room + WebSocket Connection

A participant joins an existing room via HTTP, then opens a WebSocket for real-time updates.

```mermaid
sequenceDiagram
    actor User as Client (Browser)
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant IR as IdentityResolver
    participant PS as ProfileService
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    participant FC as FusionCache + Redis
    participant WS as WebSocketBroadcaster
    participant WSE as WebSocketEndpoint

    User->>GW: POST /estimation/rooms/{roomId}/join<br/>{ displayName? }
    GW->>EC: Route to EstimationService
    EC->>IR: ResolveAsync(httpContext, displayName)
    IR->>PS: GET /profiles/{userId}
    PS-->>IR: { displayName }
    IR-->>EC: ParticipantIdentity

    EC->>SVC: JoinRoomAsync(roomId, participantId, displayName, ...)
    SVC->>DB: SELECT Room (with participants)

    alt Participant already exists & display name changed
        SVC->>DB: UPDATE display name across ALL rooms
        SVC->>FC: Invalidate room cache (backplane → all pods)
        SVC->>FC: Publish cross-pod broadcast (Redis pub/sub)
        FC-->>WS: Each pod broadcasts to local WebSocket connections
    else New participant
        SVC->>DB: INSERT Participant
    end

    SVC->>FC: Invalidate room cache + publish cross-pod broadcast
    FC-->>WS: All pods broadcast updated room state
    SVC-->>EC: Result<Room> (reloaded via cache → DB)
    EC-->>GW: 200 OK + RoomResponse
    GW-->>User: RoomResponse

    Note over User,WSE: Client now opens WebSocket for real-time updates

    User->>GW: WS /estimation/rooms/{roomId}/ws
    GW->>WSE: WebSocket upgrade
    WSE->>IR: ResolveAsync(httpContext)
    IR-->>WSE: ParticipantIdentity
    WSE->>SVC: GetRoomByIdAsync(roomId)
    SVC->>FC: Get room (L1 → L2 Redis → DB)
    FC-->>SVC: Room entity
    SVC-->>WSE: Room
    WSE->>WS: AddConnection(roomId, participantId, socket)
    WSE->>User: Initial RoomResponse snapshot (JSON)

    loop Keep-alive (read-only receive loop)
        User-->>WSE: Ping / no-op (client messages ignored)
    end
```

### 3. Voting Round (Submit Vote → Reveal → Reset)

The core estimation flow: participants vote, the moderator reveals results, then optionally starts a new round.

```mermaid
sequenceDiagram
    actor P1 as Participant 1
    actor P2 as Participant 2
    actor Mod as Moderator
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    participant WS as WebSocketBroadcaster

    Note over P1,WS: Room status: Voting (Round N)

    %% Participant 1 votes
    P1->>GW: POST /estimation/rooms/{id}/votes { value: "5" }
    GW->>EC: Route
    EC->>SVC: SubmitVoteAsync(roomId, p1Id, "5")
    SVC->>DB: Validate deck contains "5"
    SVC->>DB: DELETE existing vote (same round) + INSERT new vote
    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>P1: Snapshot (own vote="5" visible, others hidden)
    WS-->>P2: Snapshot (P1 hasVoted=true, vote hidden)
    WS-->>Mod: Snapshot (P1 hasVoted=true, vote hidden)
    SVC-->>EC: Result<Room>
    EC-->>P1: 200 OK + RoomResponse

    %% Participant 2 votes
    P2->>GW: POST /estimation/rooms/{id}/votes { value: "8" }
    GW->>EC: Route
    EC->>SVC: SubmitVoteAsync(roomId, p2Id, "8")
    SVC->>DB: DELETE + INSERT vote
    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>P1: Snapshot (P2 hasVoted=true, vote hidden)
    WS-->>P2: Snapshot (own vote="8" visible)
    WS-->>Mod: Snapshot (both hasVoted=true)
    SVC-->>EC: Result<Room>
    EC-->>P2: 200 OK + RoomResponse

    %% Moderator reveals
    Mod->>GW: POST /estimation/rooms/{id}/reveal
    GW->>EC: Route (requires [Authorize])
    EC->>SVC: RevealVotesAsync(roomId, moderatorId)
    SVC->>DB: Verify moderator + status=Voting
    SVC->>DB: Calculate distribution + average
    SVC->>DB: INSERT RoundHistory { distribution, average }
    SVC->>DB: UPDATE Room status → Revealed
    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>P1: Snapshot (all votes visible, distribution, average)
    WS-->>P2: Snapshot (all votes visible, distribution, average)
    WS-->>Mod: Snapshot (all votes visible, distribution, average)
    SVC-->>EC: Result<Room>
    EC-->>Mod: 200 OK + RoomResponse

    Note over P1,WS: Room status: Revealed

    %% Moderator resets for new round
    Mod->>GW: POST /estimation/rooms/{id}/reset
    GW->>EC: Route (requires [Authorize])
    EC->>SVC: ResetRoundAsync(roomId, moderatorId)
    SVC->>DB: Verify moderator + status=Revealed
    SVC->>DB: DELETE all votes for this room
    SVC->>DB: UPDATE Room: roundNumber++, status → Voting
    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>P1: Snapshot (Round N+1, no votes, status=Voting)
    WS-->>P2: Snapshot (Round N+1, no votes, status=Voting)
    WS-->>Mod: Snapshot (Round N+1, no votes, status=Voting)
    SVC-->>EC: Result<Room>
    EC-->>Mod: 200 OK + RoomResponse

    Note over P1,WS: Room status: Voting (Round N+1)
```

### 4. WebSocket Disconnect (Auto-Leave)

When a participant's WebSocket disconnects, they are automatically removed from the room.

```mermaid
sequenceDiagram
    actor User as Participant
    participant WSE as WebSocketEndpoint
    participant WS as WebSocketBroadcaster
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    actor Others as Other Participants

    Note over User,WSE: WebSocket connection active

    User--xWSE: Connection closed / abrupt disconnect

    WSE->>WS: RemoveConnection(roomId, participantId)
    WSE->>SVC: LeaveRoomAsync(roomId, participantId)<br/>(new DI scope)
    SVC->>DB: DELETE votes for participant
    SVC->>DB: DELETE participant from room
    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>Others: Updated RoomResponse (participant removed, all pods)

    WSE->>User: CloseAsync (if socket still open)
```

### 5. Spectator Mode Toggle

A participant switches between voter and spectator mode. Switching to spectator clears their current vote.

```mermaid
sequenceDiagram
    actor User as Participant
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    participant WS as WebSocketBroadcaster
    actor Others as Other Participants

    User->>GW: POST /estimation/rooms/{id}/mode<br/>{ isSpectator: true }
    GW->>EC: Route
    EC->>SVC: ChangeModeAsync(roomId, participantId, true)
    SVC->>DB: UPDATE participant.isSpectator = true

    alt Room status is Voting
        SVC->>DB: DELETE participant's vote for current round
    end

    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>User: Snapshot (isSpectator=true, no vote)
    WS-->>Others: Snapshot (participant now spectator, all pods)
    SVC-->>EC: Result<Room>
    EC-->>User: 200 OK + RoomResponse
```

### 6. Room Archival

The moderator permanently closes the room. No further mutations are allowed.

```mermaid
sequenceDiagram
    actor Mod as Moderator
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    participant WS as WebSocketBroadcaster
    actor Others as All Participants

    Mod->>GW: POST /estimation/rooms/{id}/archive
    GW->>EC: Route (requires [Authorize])
    EC->>SVC: ArchiveRoomAsync(roomId, moderatorId)
    SVC->>DB: Verify moderator
    SVC->>DB: UPDATE Room: status → Archived, archivedAtUtc = now
    SVC->>SVC: Invalidate room + user rooms cache (FusionCache)
    SVC->>SVC: Publish cross-pod broadcast (Redis pub/sub)
    WS-->>Others: Final RoomResponse (status=Archived, isReadOnly=true, all pods)
    SVC-->>EC: Result<Room>
    EC-->>Mod: 200 OK + RoomResponse
```

### 7. Legacy Guest Claim (Cookie → Authenticated User)

An authenticated user claims participation history from a legacy guest cookie.

```mermaid
sequenceDiagram
    actor User as Authenticated User
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant IR as IdentityResolver
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL

    User->>GW: POST /estimation/claim-guest<br/>(JWT + overflow_guest_id cookie)
    GW->>EC: Route (requires [Authorize])
    EC->>EC: Extract userId from JWT
    EC->>EC: Extract guestId from cookie

    EC->>IR: ResolveAsync(httpContext)
    IR-->>EC: ParticipantIdentity (authenticated)

    EC->>SVC: ClaimGuestAsync(guestId, userId, displayName)
    SVC->>DB: SELECT all participants WHERE guestId = {guestId}

    loop For each guest participation
        alt User already in that room
            SVC->>DB: Migrate votes (where no conflict)
            SVC->>DB: DELETE guest participant
        else User not in that room
            SVC->>DB: UPDATE participant: userId, guestId=null, isGuest=false
            SVC->>DB: UPDATE votes: participantId → userId
        end
    end

    SVC-->>EC: claimed count
    EC->>EC: Delete overflow_guest_id cookie
    EC-->>GW: 200 OK { claimed: N }
    GW-->>User: { claimed: N }
```

### 8. Clear Vote

A participant retracts their vote during an active voting round.

```mermaid
sequenceDiagram
    actor User as Participant
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    participant WS as WebSocketBroadcaster
    actor Others as Other Participants

    User->>GW: DELETE /estimation/rooms/{id}/votes/me
    GW->>EC: Route
    EC->>SVC: ClearVoteAsync(roomId, participantId)
    SVC->>DB: Verify status = Voting
    SVC->>DB: DELETE vote WHERE roomId, participantId, roundNumber
    SVC->>SVC: Invalidate cache + cross-pod broadcast (Redis)
    WS-->>User: Snapshot (no vote)
    WS-->>Others: Snapshot (participant hasVoted=false, all pods)
    SVC-->>EC: UnitResult (success)
    EC-->>User: 204 No Content
```

### 9. Refresh Profile (Instant Avatar/Name Push)

After editing their profile or avatar, a user calls this endpoint to push changes to all open rooms instantly.

```mermaid
sequenceDiagram
    actor User as Authenticated User
    participant GW as YARP Gateway
    participant EC as RoomsController
    participant PC as ProfileServiceClient
    participant IR as IdentityResolver
    participant PS as ProfileService
    participant SVC as EstimationRoomService
    participant DB as PostgreSQL
    participant FC as FusionCache + Redis
    participant WS as WebSocketBroadcaster
    actor Others as Other Participants

    User->>GW: POST /estimation/refresh-profile<br/>(JWT Bearer)
    GW->>EC: Route (requires [Authorize])
    EC->>PC: InvalidateAsync(userId)<br/>evict stale profile from FusionCache
    PC->>FC: Remove cache key "profile:{userId}"
    FC-->>PC: Evicted (backplane → all pods)

    EC->>IR: ResolveAsync(httpContext)
    IR->>PS: GET /profiles/{userId}<br/>(fresh fetch, cache was just cleared)
    PS-->>IR: { displayName, avatarUrl }
    IR-->>EC: ParticipantIdentity (fresh)

    EC->>SVC: RefreshParticipantProfileAsync(userId, displayName, avatarUrl)
    SVC->>DB: UPDATE all participants WHERE userId<br/>SET displayName, avatarUrl
    SVC->>FC: Invalidate room caches for affected rooms
    SVC->>FC: Publish cross-pod broadcast (Redis pub/sub)
    WS-->>Others: Updated snapshots (new name/avatar, all pods)
    SVC-->>EC: updated count
    EC-->>GW: 200 OK { updated: N }
    GW-->>User: { updated: N }
```

### 10. Full Session Lifecycle (End-to-End)

A complete planning poker session from room creation through multiple rounds to archival.

```mermaid
sequenceDiagram
    actor Mod as Moderator
    actor P1 as Participant 1
    actor P2 as Participant 2
    participant API as EstimationService
    participant WS as WebSocket

    Note over Mod,WS: === Phase 1: Setup ===

    Mod->>API: POST /estimation/rooms { title: "Sprint 42" }
    API-->>Mod: 201 { roomId, canonicalUrl }

    Mod->>API: WS /estimation/rooms/{id}/ws
    API-->>Mod: Initial snapshot (1 participant)

    Note over Mod: Moderator shares room link

    P1->>API: POST /estimation/rooms/{id}/join
    API-->>P1: 200 RoomResponse
    P1->>API: WS /estimation/rooms/{id}/ws
    API-->>P1: Initial snapshot
    WS-->>Mod: Updated snapshot (2 participants)

    P2->>API: POST /estimation/rooms/{id}/join
    API-->>P2: 200 RoomResponse
    P2->>API: WS /estimation/rooms/{id}/ws
    API-->>P2: Initial snapshot
    WS-->>Mod: Updated snapshot (3 participants)
    WS-->>P1: Updated snapshot (3 participants)

    Note over Mod,WS: === Phase 2: Voting Round 1 ===

    Mod->>API: POST /votes { value: "5" }
    WS-->>P1: Snapshot (Mod hasVoted)
    WS-->>P2: Snapshot (Mod hasVoted)

    P1->>API: POST /votes { value: "8" }
    WS-->>Mod: Snapshot (P1 hasVoted)
    WS-->>P2: Snapshot (P1 hasVoted)

    P2->>API: POST /votes { value: "5" }
    WS-->>Mod: Snapshot (all voted)
    WS-->>P1: Snapshot (all voted)

    Mod->>API: POST /reveal
    WS-->>Mod: Snapshot (votes: 5,8,5 — avg 6.0)
    WS-->>P1: Snapshot (votes: 5,8,5 — avg 6.0)
    WS-->>P2: Snapshot (votes: 5,8,5 — avg 6.0)

    Note over Mod,WS: === Phase 3: Round 2 (re-estimate) ===

    Mod->>API: POST /reset
    WS-->>Mod: Snapshot (Round 2, Voting, no votes)
    WS-->>P1: Snapshot (Round 2, Voting, no votes)
    WS-->>P2: Snapshot (Round 2, Voting, no votes)

    Mod->>API: POST /votes { value: "8" }
    P1->>API: POST /votes { value: "8" }
    P2->>API: POST /votes { value: "8" }

    Mod->>API: POST /reveal
    WS-->>Mod: Snapshot (consensus: 8!)
    WS-->>P1: Snapshot (consensus: 8!)
    WS-->>P2: Snapshot (consensus: 8!)

    Note over Mod,WS: === Phase 4: Cleanup ===

    Mod->>API: POST /archive
    WS-->>Mod: Final snapshot (Archived, read-only)
    WS-->>P1: Final snapshot (Archived, read-only)
    WS-->>P2: Final snapshot (Archived, read-only)

    P1--xAPI: WebSocket disconnect → auto-leave
    P2--xAPI: WebSocket disconnect → auto-leave
    Mod--xAPI: WebSocket disconnect → auto-leave
```

### Room State Machine

```mermaid
stateDiagram-v2
    [*] --> Voting : Room created (Round 1)

    Voting --> Revealed : Moderator reveals votes
    Revealed --> Voting : Moderator resets (Round N+1)
    Voting --> Archived : Moderator archives
    Revealed --> Archived : Moderator archives

    Archived --> [*]

    note right of Voting
        Participants can:
        - Submit/change/clear votes
        - Join/leave room
        - Toggle spectator mode
    end note

    note right of Revealed
        All votes visible.
        Distribution + average calculated.
        Round saved to history.
    end note

    note right of Archived
        Read-only. No mutations.
        Room permanently closed.
    end note
```

---

## Endpoints

| Method   | Route                             | Auth      | Description                                      |
|----------|-----------------------------------|-----------|--------------------------------------------------|
| `POST`   | `/estimation/rooms`               | Optional  | Create a new room (guests provide `displayName`) |
| `POST`   | `/estimation/rooms/{id}/join`     | Optional  | Join a room (guests provide `displayName`)       |
| `GET`    | `/estimation/rooms/{id}`          | None      | Get current room state                           |
| `GET`    | `/estimation/rooms/my`            | Required  | List rooms for the authenticated user            |
| `POST`   | `/estimation/rooms/{id}/votes`    | Required* | Submit or replace a vote                         |
| `DELETE` | `/estimation/rooms/{id}/votes/me` | Required* | Clear your vote                                  |
| `POST`   | `/estimation/rooms/{id}/reveal`   | Moderator | Reveal all votes                                 |
| `POST`   | `/estimation/rooms/{id}/reset`    | Moderator | Start a new round                                |
| `POST`   | `/estimation/rooms/{id}/archive`  | Moderator | Permanently close the room                       |
| `POST`   | `/estimation/rooms/{id}/mode`     | Required* | Toggle spectator/voter                           |
| `POST`   | `/estimation/rooms/{id}/leave`    | Required* | Leave the room                                   |
| `POST`   | `/estimation/claim-guest`         | Required  | Migrate guest history to authenticated user      |
| `POST`   | `/estimation/refresh-profile`     | Required  | Push latest profile (name + avatar) to all rooms |
| `GET`    | `/estimation/decks`               | None      | List available card decks                        |
| `WS`     | `/estimation/rooms/{id}/ws`       | Optional  | Real-time room state push                        |

*\* Authenticated users or identified guests (via cookie)*

---

## WebSocket Protocol

- **URL:** `wss://{host}/api/estimation/rooms/{id}/ws`
- **Direction:** Server → client only (read-only push)
- **Format:** JSON — same shape as `GET /estimation/rooms/{id}` response
- **Initial message:** Full `RoomResponse` snapshot on connect
- **Subsequent:** Updated snapshot on every room state change
- **Personalized:** Each participant receives a viewer-scoped payload (own vote visible, others hidden until reveal)

---

## Participant Identity

| Type           | Identity source                  | Can create rooms | Can moderate     |
|----------------|----------------------------------|------------------|------------------|
| Authenticated  | Keycloak JWT `sub` claim         | ✅                | ✅ (if moderator) |
| Guest (new)    | Keycloak JWT (auto-created user) | ✅                | ✅ (if moderator) |
| Guest (legacy) | `overflow_guest_id` cookie       | ❌                | ❌                |

> **Note:** New guests get real Keycloak accounts created by the webapp (see `AGENTS.md` → Guest Auth).
> They are indistinguishable from regular authenticated users at the backend level.
> Legacy cookie-based guests are only supported for backwards compatibility.

---

## Project Structure

```
Overflow.EstimationService/
├── Program.cs                   # DI, EF Core, WebSocket setup
├── Controllers/
│   ├── RoomsController.cs       # All HTTP room endpoints
│   └── DecksController.cs       # Card deck listing endpoint
├── Data/
│   └── EstimationDbContext.cs   # EF Core DbContext
├── DTOs/
│   ├── Requests.cs              # CreateRoom, JoinRoom, SubmitVote, ChangeMode DTOs
│   └── Responses.cs             # RoomResponse, ParticipantResponse, RoundSummary, etc.
├── Models/
│   ├── EstimationRoom.cs        # Room entity + RoomStatus enum
│   ├── EstimationParticipant.cs # Participant entity
│   ├── EstimationVote.cs        # Vote entity
│   ├── EstimationRoundHistory.cs# Round history entity
│   └── DeckDefinition.cs       # Deck definitions (Fibonacci, etc.)
├── Services/
│   ├── EstimationRoomService.cs     # Room business logic (all mutations)
│   ├── RoomCacheService.cs          # FusionCache layer (L1 + L2 Redis) for room reads
│   ├── CrossPodBroadcastService.cs  # Redis pub/sub for cross-pod WS broadcast
│   ├── WebSocketBroadcaster.cs      # WS connection tracking + viewer-scoped broadcast
│   └── ArchivedRoomCleanupService.cs# Background job: deletes expired archived rooms
├── Options/
│   └── RoomCleanupOptions.cs        # IOptions for room cleanup (RetentionDays, IntervalHours)
├── Auth/
│   ├── IdentityResolver.cs      # JWT → user, cookie → guest resolution
│   └── GuestIdentity.cs         # Guest cookie issuance + reading
├── Clients/
│   └── ProfileServiceClient.cs  # HTTP client for display name resolution (60s cache)
├── Extensions/
│   └── WebSocketEndpoints.cs    # WebSocket endpoint registration + disconnect handling
├── Mapping/
│   └── RoomResponseMapper.cs    # Entity → viewer-scoped RoomResponse (vote visibility rules)
├── Exceptions/
│   └── RoomErrors.cs            # Domain errors (NotFound, Archived, Forbidden, etc.)
├── Migrations/                  # EF Core migrations
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Staging.json
├── appsettings.Production.json
└── Dockerfile
```

---

## Configuration

| Key                                  | Source                   | Description                                                                                         |
|--------------------------------------|--------------------------|-----------------------------------------------------------------------------------------------------|
| `ConnectionStrings:estimationDb`     | ConfigMap / Infisical    | PostgreSQL connection string                                                                        |
| `ConnectionStrings:estimation-redis` | Aspire (dev only)        | Redis — auto-injected by Aspire in dev                                                              |
| `ConnectionStrings:Redis`            | Infisical (staging/prod) | Redis — `CONNECTION_STRINGS__REDIS` from Infisical `/app/connections` (includes password + options) |
| `KeycloakOptions:*`                  | appsettings + ConfigMap  | JWT validation settings                                                                             |
| `APP_BASE_URL`                       | ConfigMap / Infisical    | Base URL for `canonicalUrl` in responses                                                            |
| `PROFILE_SERVICE_URL`                | Aspire / ConfigMap       | ProfileService base URL for name resolution                                                         |
| `RoomCleanup:RetentionDays`          | appsettings / Infisical  | Days before archived rooms are deleted (default: 30)                                                |
| `RoomCleanup:IntervalHours`          | appsettings / Infisical  | Cleanup job run interval in hours (default: 24)                                                     |

> **Redis connection string format** (staging/prod):
`redis.infra-production.svc.cluster.local:6379,password=...,abortConnect=false`  
> In Infisical, stored as `CONNECTION_STRINGS__REDIS` in `/app/connections` (maps to `ConnectionStrings:Redis` in .NET
> config, case-insensitive).

---

## EF Core Migrations

```bash
dotnet ef migrations add <MigrationName> \
  --project Overflow.EstimationService \
  --context EstimationDbContext
```

Migrations run automatically at startup.

---

## Local Development

### With Aspire

```bash
cd Overflow.AppHost && dotnet run
```

The YARP gateway routes `/estimation/{**catch-all}` to the service.

### WebSocket testing

```bash
# Using wscat (npm i -g wscat)
wscat -c "ws://localhost:8001/estimation/rooms/{roomId}/ws" \
  --header "Cookie: overflow_guest_id=guest_abc123"
```

---

## Possible Improvements

- **Add room history and replay** — Store completed round results (votes, average, consensus) in a `RoundHistory` table
  and expose `GET /estimation/rooms/{id}/history`. This lets teams review past estimations and track estimation accuracy
  over time.
- **Support user-defined custom card decks** — Currently decks are predefined server-side (Fibonacci, T-Shirts).
  Allowing moderators to define fully custom card values at room creation would make the tool more flexible for
  different estimation methodologies.

---

## Related Documentation

- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture
- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — Auth configuration

