# EstimationService (Planning Poker)

Real-time Planning Poker estimation rooms with WebSocket push.

---

## Overview

|               |                                                                |
|---------------|----------------------------------------------------------------|
| **Type**      | .NET 10 ASP.NET Core (Controllers + WebSocket endpoint)        |
| **Database**  | PostgreSQL via EF Core (`estimationDb`)                        |
| **Real-time** | Raw WebSocket (server → client snapshots)                      |
| **Auth**      | Keycloak JWT — guests get real Keycloak accounts automatically |
| **Port**      | 8080                                                           |
| **Messaging** | None — no Wolverine or RabbitMQ dependency                     |

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

---

## Endpoints

| Method   | Route                             | Auth      | Description                                      |
|----------|-----------------------------------|-----------|--------------------------------------------------|
| `POST`   | `/estimation/rooms`               | Optional  | Create a new room (guests provide `displayName`) |
| `POST`   | `/estimation/rooms/{id}/join`     | Optional  | Join a room (guests provide `displayName`)       |
| `GET`    | `/estimation/rooms/{id}`          | None      | Get current room state                           |
| `POST`   | `/estimation/rooms/{id}/votes`    | Required* | Submit or replace a vote                         |
| `DELETE` | `/estimation/rooms/{id}/votes/me` | Required* | Clear your vote                                  |
| `POST`   | `/estimation/rooms/{id}/reveal`   | Moderator | Reveal all votes                                 |
| `POST`   | `/estimation/rooms/{id}/reset`    | Moderator | Start a new round                                |
| `POST`   | `/estimation/rooms/{id}/archive`  | Moderator | Permanently close the room                       |
| `POST`   | `/estimation/rooms/{id}/mode`     | Required* | Toggle spectator/voter                           |
| `POST`   | `/estimation/rooms/{id}/leave`    | Required* | Leave the room                                   |
| `POST`   | `/estimation/claim-guest`         | Required  | Migrate guest history to authenticated user      |
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
│   └── RoomsController.cs       # All HTTP room endpoints
├── Data/
│   └── EstimationDbContext.cs   # EF Core DbContext
├── DTOs/
│   └── EstimationDtos.cs       # Request/response DTOs (RoomResponse, etc.)
├── Models/                      # Room, Participant, Vote, RoundHistory entities
├── Services/
│   ├── EstimationRoomService.cs # Room business logic
│   ├── WebSocketBroadcaster.cs  # WS connection tracking + broadcast
│   ├── IdentityResolver.cs     # JWT → user, cookie → guest resolution
│   ├── GuestIdentity.cs        # Guest cookie issuance + reading
│   ├── ProfileServiceClient.cs # HTTP client for display name resolution
│   └── RoomResponseMapper.cs   # Entity → viewer-scoped RoomResponse
├── Extensions/
│   └── WebSocketEndpoints.cs   # WebSocket endpoint registration
├── Mapping/                     # Entity mapping helpers
├── Exceptions/                  # Domain exceptions
├── Migrations/                  # EF Core migrations
├── Auth/                        # Auth helpers
├── Clients/                     # External service clients
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Staging.json
├── appsettings.Production.json
└── Dockerfile
```

---

## Configuration

| Key                              | Source                  | Description                              |
|----------------------------------|-------------------------|------------------------------------------|
| `ConnectionStrings:estimationDb` | ConfigMap / Infisical   | PostgreSQL connection string             |
| `KeycloakOptions:*`              | appsettings + ConfigMap | JWT validation settings                  |
| `APP_BASE_URL`                   | ConfigMap / Infisical   | Base URL for `canonicalUrl` in responses |

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
- **Add room expiration with automatic cleanup** — Rooms currently persist indefinitely. Adding a configurable TTL (
  e.g., 24 hours of inactivity) with a background `IHostedService` that archives stale rooms would prevent database
  bloat and keep the rooms list manageable.
- **Support custom card decks** — Currently decks are predefined server-side. Allowing moderators to define custom card
  values (e.g., t-shirt sizes, risk levels) at room creation would make the tool more flexible for different estimation
  methodologies.

---

## Related Documentation

- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture
- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — Auth configuration

