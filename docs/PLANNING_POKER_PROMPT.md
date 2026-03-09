# Overflow — Claude IDE Prompt for Planning Poker

Use this prompt with Claude Sonnet/Opus in your IDE to add a Kollabe-style Planning Poker feature to the Overflow repo.

---

## Confirmed product decisions used in this prompt

These decisions are now confirmed and should be treated as requirements, not suggestions.

- Only authenticated users can create rooms.
- Guests can join by room code or direct room link in v1.
- Guests should be able to participate in the room flow, not just spectate.
- All joiners can switch into **Spectator** mode.
- Spectators can watch the room in real time but **cannot vote**.
- Spectators must **not block** the round flow or reveal logic.
- Guests can join and vote with a **server-issued guest cookie** that survives browser refreshes.
- No attachment to questions, tickets, or external tools in v1.
- A room is a standalone estimation session with a moderator (the creator).
- Moderator shares screen externally on a call; the app only handles room state and voting.
- Default deck is **classic Fibonacci only** for v1, but the backend and frontend must be designed to support future custom decks without a redesign.
- Each room has a **room code**, **room title**, and **unique URL**.
- Guests entering through a direct room URL can join immediately after providing a guest name.
- Show **revealed cards + vote distribution + numeric average** after reveal.
- Numeric average should be rounded to **1 decimal place** for display.
- UI scope is **current round only** in v1.
- Use **Marten as the event store/source of truth for estimation rooms** so future round history and audit trails are preserved naturally through persisted room event streams.
- Persist domain events per room stream and project current room state from those events.
- This makes future room/round history and audit trails natural.
- Moderator stays fixed to the room creator; if the creator leaves, everyone waits until they return.
- Archived rooms are **read-only**, not deleted or hidden.
- Landing page scope is **Create room / Join room** only. Recent rooms are future scope.
- UI terminology should be **Planning Poker**.
- There is no rush; prefer a **solid, production-shaped implementation plan** over the fastest shortcut.
- Use **raw ASP.NET Core WebSockets** for live room state. Polling is only an optional fallback, not the primary architecture.
- In .NET 10, `System.Net.WebSockets.WebSocketStream` may be used on the backend as a helper abstraction over `WebSocket` where it simplifies streaming reads/writes.

---

## Prompt

You are working inside the **Overflow** repository, a microservices application with:

- backend: `.NET 10`, ASP.NET Core, EF Core/Marten, PostgreSQL, Keycloak auth, OpenTelemetry
- frontend: `Next.js 16 App Router`, `React 19`, `TypeScript`, `Tailwind`, `HeroUI`, `next-auth`
- infra: `.NET Aspire` for local dev, `Kustomize` in `k8s/`, `Terraform` in `terraform/`

Your goal is to add a **new Estimation Service** and a **Planning Poker feature** in the webapp that provides functionality similar to Kollabe planning poker, but **without any integration to questions/issues/tickets**.

The user story is simple:

- one authenticated user creates a room and becomes the moderator
- the moderator shares their screen during a call
- other users can join the room from the webapp, including guests via room code or room link
- everyone selects a card privately
- moderator reveals cards
- moderator resets the round and the group iterates

Do not over-design beyond that. Build a polished v1 that matches this repo’s style and conventions while keeping the backend **event-sourced with Marten** so future history is natural.

---

## Non-negotiable implementation constraints

1. **Create a separate backend service** named `Overflow.EstimationService`.
2. **Do not** attach estimation rooms to questions or any existing domain object.
3. **Do not** add external integrations for Jira, Linear, GitHub, etc.
4. Use **realtime communication** for room state updates. Prefer **raw ASP.NET Core WebSockets** for this repo. Keep HTTP polling only as a fallback/resilience mechanism, not the primary design.
5. Keep auth consistent with the existing Keycloak / NextAuth setup for authenticated users, and add a clean guest participation flow for room join.
6. Use **Marten as the event store/source of truth** for estimation rooms. Persist domain events per room stream and derive current room state from projections/read models built from those events.
7. Keep the implementation production-shaped:
   - proper DTOs/models/services
   - persisted room and round state
   - health checks
   - local Aspire wiring
   - k8s + terraform updates
   - frontend navigation and room pages
   - realtime room updates
8. Preserve existing patterns and naming. Avoid unrelated refactors.

---

## Inspect these files first before making changes

### Repository / architecture
- `README.md`
- `Overflow.slnx`
- `Directory.Build.props`
- `Directory.Packages.props`

### Backend patterns
- `Overflow.AppHost/AppHost.cs`
- `Overflow.QuestionService/Program.cs`
- `Overflow.VoteService/Program.cs`
- `Overflow.Common/`
- `Overflow.ServiceDefaults/`
- `Overflow.Contracts/`

### Frontend patterns
- `webapp/package.json`
- `webapp/src/app/(main)/layout.tsx`
- `webapp/src/components/SideMenu.tsx`
- `webapp/src/lib/fetchClient.ts`
- `webapp/src/lib/config.ts`
- `webapp/src/lib/actions/question-actions.ts`
- `webapp/src/lib/types/index.ts`
- `webapp/src/auth.ts`

### Infra patterns
- `k8s/README.md`
- `k8s/base/*`
- `k8s/overlays/staging/kustomization.yaml`
- `k8s/overlays/staging/ingress.yaml`
- `k8s/overlays/production/*`
- `terraform/data.tf`
- `terraform/main.tf`
- `terraform/README.md`

---

## Product contract for v1

Implement a standalone Planning Poker feature with the following behavior.

### Room lifecycle
- Only an authenticated user can create a room.
- Room gets a unique shareable code and URL.
- Room includes a required title.
- Creator becomes moderator.
- Other users can join by visiting the room URL or entering the room code.
- Guest users can join by room URL/code too.
- Direct room URL access should allow immediate entry after guest name is provided.
- Any joined user/guest can switch between **Participant** mode and **Spectator** mode.
- Spectators remain in the room and receive live updates, but cannot cast votes.
- Room has an active round.
- Users in participant mode can pick exactly one card per round.
- Votes remain hidden until reveal.
- Moderator can reveal votes.
- Moderator can reset/start next round.
- Users can change their vote before reveal.
- Users can leave a room.
- Moderator can close/archive the room.
- Archived rooms remain viewable as read-only.

### Visibility rules
- Before reveal:
  - each voting participant sees their own selected card
  - all users see which participants have voted
  - spectators are visible as spectators, not as “waiting for vote” participants
  - nobody except the voter sees the actual card values
- After reveal:
  - all participants and spectators see every revealed vote
  - show revealed cards, vote distribution, and numeric average when the revealed values are numeric

### Moderator permissions
Only the room creator/moderator can:
- reveal the round
- reset/start next round
- archive/close the room

### Guest expectations
- guests can join by room code/link
- guests must provide a display name before entering the room
- guest identity should survive browser refresh/reconnect via a **server-issued guest cookie**
- guests can switch between participant mode and spectator mode
- guest permissions inside a live room are the same as non-moderator participants when in participant mode
- guests can never become moderator in v1

### Spectator mode expectations
- any joined user can switch to spectator mode
- spectator mode disables voting UI and vote submission server-side
- spectator mode does not remove the user from the room
- spectator mode does not count toward “who still needs to vote”
- switching back from spectator to participant mode is allowed while the room is active
- if a user switches to spectator after already voting in the current hidden round, the implementation should choose one simple, explicit rule and document it; preferred rule: clear their unrevealed vote when switching to spectator

### Nice v1 UX expectations
- good empty states
- clear “copied room link/code” affordance
- clear current status (`Voting`, `Revealed`, `Archived`)
- distinguish moderator from participants
- clearly distinguish authenticated users from guests if helpful
- clearly distinguish spectators from voting participants
- mobile-friendly enough to be usable, desktop-first is okay

---

## Architecture decision for v1

Use this implementation strategy unless repository realities force a small adjustment:

### Backend transport
- Use normal REST endpoints on the Estimation Service for commands/mutations.
- Persist estimation room **events** in PostgreSQL via **Marten**.
- Use **Marten** as the event store/document database for this service.
- Treat each room's Marten event stream as the **source of truth**.
- Serve current room state through projections/read models derived from those events.
- Expose a **raw WebSocket endpoint** for room subscriptions and live updates.

### Backend event model
Use **event sourcing with Marten** so the service is naturally history-ready.

Suggested event stream approach:
- one stream per room
- append room/round/participant/vote events to that stream
- project current room state for efficient reads

Suggested events:
- `RoomCreated`
- `GuestJoinedRoom`
- `AuthenticatedUserJoinedRoom`
- `ParticipantModeChanged`
- `ParticipantLeftRoom`
- `VoteSubmitted`
- `VoteCleared`
- `VotesRevealed`
- `RoundReset`
- `RoomArchived`
- optional: `RoomTitleUpdated` and `DeckConfigured` if it helps keep future extensibility clean

Important: use event sourcing where it adds value, but do not let the service become over-complicated. Keep the read side straightforward.

---

## Part 1 — Backend: new Estimation Service

Create a new service project named `Overflow.EstimationService`.

### Service responsibilities
The new service owns:
- room creation
- guest and authenticated participant membership
- participant/spectator mode
- room state
- vote submission/update
- reveal/reset/archive actions
- current room queries
- room event streams as the source of truth
- projected current room state
- audit/history data for future evolution

It should **not** own:
- voice/video calls
- ticket or question attachments
- cross-service orchestration beyond what is needed internally
- external notifications

### Suggested project structure
Follow existing backend conventions and keep the structure close to the other services:

- `Overflow.EstimationService/Program.cs`
- `Overflow.EstimationService/Overflow.EstimationService.csproj`
- `Overflow.EstimationService/Data/`
- `Overflow.EstimationService/DTOs/`
- `Overflow.EstimationService/Models/`
- `Overflow.EstimationService/Services/`
- `Overflow.EstimationService/Projections/`
- `Overflow.EstimationService/Properties/`
- `Overflow.EstimationService/appsettings.json`
- `Overflow.EstimationService/appsettings.Staging.json`
- `Overflow.EstimationService/appsettings.Production.json`
- `Overflow.EstimationService/Dockerfile`

### Recommended domain and projection model
Keep the write side **event-sourced** and the read side simple.

#### Event stream identity
Use one Marten stream per room:
- stream id: room id or room code-backed stable identifier

#### Events
Define event contracts local to the service for room evolution.

Suggested events:
- `RoomCreated`
- `ParticipantJoined`
- `ParticipantModeChanged`
- `ParticipantLeft`
- `VoteSubmitted`
- `VoteCleared`
- `VotesRevealed`
- `RoundReset`
- `RoomArchived`

#### Read model: `EstimationRoomView`
Project a current-state document from the room event stream with fields like:
- `Id: Guid`
- `Code: string`
- `Title: string`
- `ModeratorUserId: string?`
- `ModeratorGuestId: string?` (likely null in v1 because creator is authenticated)
- `DeckType: string`
- `DeckValues: string[]`
- `Status: string` (`Voting`, `Revealed`, `Archived`)
- `RoundNumber: int`
- `CreatedAtUtc: DateTime`
- `UpdatedAtUtc: DateTime`
- `ArchivedAtUtc: DateTime?`
- `Participants: List<ParticipantView>`
- `Votes: List<RoundVoteView>` for the current round only in the main projection

#### `ParticipantView`
Suggested fields:
- `ParticipantId: string`
- `UserId: string?`
- `GuestId: string?`
- `DisplayName: string`
- `IsGuest: bool`
- `IsModerator: bool`
- `IsSpectator: bool`
- `JoinedAtUtc: DateTime`
- `LastSeenAtUtc: DateTime`
- `LeftAtUtc: DateTime?`

#### `RoundVoteView`
Suggested fields:
- `RoundNumber: int`
- `ParticipantId: string`
- `Value: string`
- `SubmittedAtUtc: DateTime`

### Deck design
- v1 UI uses **classic Fibonacci only**.
- Do **not** hardcode the system in a way that blocks future custom decks.
- Make deck handling data-driven, e.g. a deck definition object with id/name/values.
- It is acceptable if only one deck is enabled in v1, as long as backend/frontend contracts support future expansion.

### Marten / projections
Follow the repo’s Marten usage style where appropriate, but keep this service simpler than `stats-svc`.

Suggested approach:
- Marten event store for room streams
- one room stream is the source of truth for one estimation room
- append domain events to that stream from explicit service-layer methods
- build current room state through inline projections or another simple projection mode
- query the projected room document for current-state reads
- keep history/audit retrieval possible directly from the persisted event stream for future features

### Auth / authorization
- Require authentication for room creation.
- Allow guest room join by code/link.
- Resolve authenticated user from Keycloak claims the same way other services do.
- Resolve guest identity from a **server-issued guest cookie** and/or a guest connection token/header forwarded by the webapp.
- Enforce moderator-only behavior in the service layer, not only the UI.
- Apply the same identity model to websocket connections and REST mutations.
- Enforce spectator-mode restrictions server-side so spectators cannot vote even if they bypass the UI.

### API shape
Use minimal APIs or controllers, but keep them clean and consistent. A minimal API layout is fine for this service.

Recommended endpoints:

- `POST /estimation/rooms`
  - authenticated only
  - create room
  - request: required room title, optional deck type
  - response: created room snapshot

- `POST /estimation/rooms/{code}/join`
  - authenticated or guest
  - for guests, accept display name on first join and establish guest identity
  - idempotent if already joined
  - refresh `LastSeenAtUtc`

- `POST /estimation/rooms/{code}/mode`
  - authenticated or guest participant
  - switch between `Participant` and `Spectator`
  - if switching to spectator before reveal, prefer clearing any unrevealed vote for the current round

- `POST /estimation/rooms/{code}/leave`
  - authenticated or guest
  - mark current participant as left

- `GET /estimation/rooms/{code}`
  - authenticated or guest participant
  - return full room state tailored to current viewer
  - include participants, whether each participant has voted, current viewer vote, reveal status, round number, and revealed votes if applicable
  - if room is archived, return a read-only state rather than blocking access

- `POST /estimation/rooms/{code}/votes`
  - authenticated or guest participant
  - create/update current participant vote for current round
  - request: selected card value

- `DELETE /estimation/rooms/{code}/votes/me`
  - authenticated or guest participant
  - clear current participant vote before reveal

- `POST /estimation/rooms/{code}/reveal`
  - moderator only
  - changes state from `Voting` to `Revealed`

- `POST /estimation/rooms/{code}/reset`
  - moderator only
  - increments round number and clears current round via new round state

- `POST /estimation/rooms/{code}/archive`
  - moderator only
  - archive room and make it read-only

Optional low-risk endpoint if helpful:
- `GET /estimation/decks`
  - return supported deck definitions for frontend rendering

### Response contract
Design a room response DTO that makes frontend sync easy. Suggested shape:

- room metadata:
  - `code`
  - `title`
  - `canonicalUrl`
  - `status`
  - `roundNumber`
  - `deck`
  - `isArchived`
  - `isReadOnly`
- current viewer metadata:
  - `participantId`
  - `userId?`
  - `guestId?`
  - `displayName`
  - `isGuest`
  - `isModerator`
  - `isSpectator`
  - `selectedVote`
- participants list:
  - `participantId`
  - `displayName`
  - `isGuest`
  - `isModerator`
  - `isSpectator`
  - `hasVoted`
  - `revealedVote` (null until revealed)
  - `isPresent` / `hasLeft`
- round summary:
  - `roundNumber`
  - `status`
  - `votesRevealed`
  - `distribution`
  - `numericAverage` (null when any revealed vote is non-numeric or no numeric votes exist)
  - `numericAverageDisplay` (rounded to 1 decimal place)
  - `activeVoterCount`
  - `spectatorCount`
  - `availableDeck`

### Business rules / edge cases
Handle these explicitly:
- guest join should require a display name on first join
- guest refresh/rejoin should reuse the same guest identity instead of duplicating participant rows
- viewing an archived room should work
- mutating an archived room should fail cleanly
- moderator cannot reveal twice without reset
- reset on archived room should fail
- users/guests may update vote while status is `Voting`
- users/guests may not vote after reveal until reset
- spectators may not vote
- spectators must not count as pending voters
- switching to spectator should not leave a stale hidden vote that blocks or distorts the round; preferred behavior is to clear the unrevealed vote
- room code collision must be retried server-side
- moderator remains the room owner even if temporarily absent
- only room creator is moderator in v1
- numeric average should be computed only when all revealed votes for the round are numeric Fibonacci values from active voters
- numeric average should be rounded to **1 decimal place** for display
- current round only is enough for the main room API, but the persisted room event stream should make future history features straightforward

### Service wiring
Update the repo so the new service is fully wired in:

#### Solution / project wiring
- add `Overflow.EstimationService` to `Overflow.slnx`
- add any package versions via `Directory.Packages.props` if needed

#### Aspire / local `AppHost` wiring
Update `Overflow.AppHost/AppHost.cs` and make the local orchestration fully work end to end.

Concrete checklist:
- add `estimationDb` to the local PostgreSQL resources next to the other service databases
- add the `Overflow.EstimationService` project registration as `estimation-svc`
- wire references/dependencies the same way the other backend services are wired (`keycloak`, `messaging`, database, wait-for dependencies as needed)
- add local gateway/YARP route wiring for the HTTP API path, consistent with the existing `gateway` routes
- add explicit local routing for the websocket endpoint too, so the browser can reach realtime updates in Aspire dev mode
- verify the local webapp path/proxy setup can reach both `/estimation/...` HTTP endpoints and the estimation websocket endpoint during local development

Suggested local routes:
- `/estimation/{**catch-all}` → `estimation-svc`
- websocket route for estimation room updates under the same local API surface

#### Kubernetes
Add a new base service under:
- `k8s/base/estimation-svc/`
  - `deployment.yaml`
  - `service.yaml`
  - `kustomization.yaml`

Update overlays:
- `k8s/overlays/staging/kustomization.yaml`
- `k8s/overlays/production/kustomization.yaml`
- `k8s/overlays/staging/ingress.yaml`
- `k8s/overlays/production/ingress.yaml`

Use a new API path:
- `/api/estimation/*` → `estimation-svc:8080/estimation/*`

Also ensure websocket upgrade/proxy behavior is preserved for the estimation realtime endpoint.

#### Terraform
Update:
- `terraform/data.tf`
- `terraform/main.tf`
- any related outputs/docs if needed

Add a new DB slot:
- `staging_estimations`
- `production_estimations`

Add config map keys:
- `ConnectionStrings__estimationDb`

### Backend quality gates
Before finishing:
- build the new service and the solution
- ensure auth/Marten/projection/websocket startup compiles cleanly
- validate no broken references in Aspire wiring
- validate local Aspire startup includes `estimation-svc`, `estimationDb`, and reachable estimation HTTP/websocket routes
- add focused tests for core room rules and/or projection behavior where practical

---

## Part 2 — Frontend: Planning Poker section and room UI

Create a new Planning Poker area in the webapp.

### Navigation
Update the side menu in `webapp/src/components/SideMenu.tsx`:
- add a new item: `Planning Poker`
- use an appropriate icon from the existing icon set
- route should be `/planning-poker`

### Route structure
Add a new route group under the main app, for example:

- `webapp/src/app/(main)/planning-poker/page.tsx`
- `webapp/src/app/(main)/planning-poker/[code]/page.tsx`

Recommended UX:

#### `/planning-poker`
Landing page with:
- short explanation of what planning poker is
- create room form with required title
- join room by code input
- required guest name input for unauthenticated users joining a room
- note that moderator shares screen externally on a call

#### `/planning-poker/[code]`
Main room page with:
- room header (room title, room code, copy link, room status, moderator badge)
- participant list
- current round info
- card deck picker
- moderator controls
- results panel after reveal
- archived room read-only state when applicable
- guest join gate that asks for a guest name before entering, when needed
- mode switch for `Participant` / `Spectator`

### Frontend integration pattern
Because current API access patterns are mostly server-side, do **not** make the browser call the Estimation Service directly for authenticated REST mutations.

Instead:

#### Add route handlers in Next.js
Create route handlers under `webapp/src/app/api/estimation/...` that proxy REST commands to backend.

They should:
- get session via existing auth helpers when present
- forward `Authorization: Bearer <token>` to the backend service for authenticated users
- manage the durable guest identity cookie for guest participants
- normalize errors into predictable JSON

#### Realtime connection
Use a direct browser connection to the Estimation Service’s **WebSocket endpoint** via the same-origin `/api/estimation/...` path.

The client should:
- subscribe to room updates after room join/bootstrap succeeds
- reconnect automatically
- use authenticated connection auth for signed-in users
- use guest cookie or guest connection token for guests
- handle normal websocket open/message/close/error states explicitly

### Frontend types and helpers
Add types to `webapp/src/lib/types/index.ts` or split into a new file if cleaner:
- `PlanningPokerRoom`
- `PlanningPokerParticipant`
- `PlanningPokerRoomState`
- `PlanningPokerDeckValue`
- `PlanningPokerStatus`
- `PlanningPokerViewer`
- `PlanningPokerParticipationMode`

Add actions/helpers, for example:
- `webapp/src/lib/actions/estimation-actions.ts`

These can be used by server components where helpful, but client polling should go through the local Next.js API routes.

### Client state and polling
Create a client component for the live room experience.

Suggested responsibilities:
- fetch initial room snapshot from local `/api/estimation/...`
- connect to the websocket endpoint for live updates
- show reconnect/loading states for the realtime connection
- show pending/loading states for vote, reveal, reset, and mode switch
- refresh immediately after a successful mutation when useful
- treat archived rooms as read-only without breaking the page
- keep an optional low-frequency polling fallback only for resilience/debugging

Do not introduce a heavy global state library for this feature unless clearly useful. Local component state is enough for v1.

### Room page UI requirements
Implement these UI pieces.

#### 1. Header
- room title
- room code
- copy room code/link button
- room status badge
- round number
- moderator marker
- read-only archived indicator when relevant

#### 2. Participants panel
For each participant show:
- display name
- moderator badge if applicable
- guest/authenticated treatment if useful
- spectator/participant badge
- voted indicator while hidden
- actual card after reveal for active voters
- optional “left room” / inactive treatment if relevant

#### 3. Participation mode switch
- clear toggle/button between `Participant` and `Spectator`
- switching to spectator disables voting UI
- switching back to participant re-enables voting if room status allows it
- explain clearly that spectators can watch but do not vote

#### 4. Card deck
Render selectable cards using the chosen deck values.

Behavior:
- use classic Fibonacci values in v1
- only enabled while room status is `Voting` and the viewer is in participant mode
- highlight the current viewer’s selected card
- allow changing selection before reveal
- allow clearing selection before reveal if implemented
- architect the component so deck values can become configurable later

#### 5. Moderator controls
Only visible/enabled for moderator:
- reveal cards
- reset / next round
- archive room

Show disabled states and reason when action is not available.

#### 6. Results panel
After reveal show:
- all revealed card values by active voters
- distribution summary
- numeric average for numeric-only rounds, rounded to **1 decimal place**
- spectators should not appear as missing votes or distort distribution/average

### Auth / access behavior
- authenticated users can create rooms
- guests can join rooms via code/link
- any joined user can switch between participant and spectator mode
- unauthenticated users on the landing page should still be able to use the guest join flow
- if room is missing, show a clear empty/error state
- if room is archived, show the room in read-only mode

### Styling expectations
Match the existing app style:
- use Tailwind + HeroUI
- keep layout consistent with current `MainLayout`
- no jarring separate design system
- keep the feature visually clean and screen-share friendly

### Frontend edge cases
Handle these cleanly:
- bad room code
- archived room
- room not found
- guest opens page without guest identity established yet
- user opens page before join completes
- duplicate clicks on reveal/reset
- switching mode while a hidden vote exists
- spectators shown as waiting for votes when they should not be
- polling response arrives after a mutation and should not clobber fresher local state unexpectedly
- authenticated user loses auth mid-room

### Frontend quality gates
Before finishing:
- run lint/type checks for the webapp
- ensure the new route is accessible from the side menu
- smoke test create → join → vote → reveal → reset flow
- smoke test guest join → vote → reveal flow
- smoke test switching between participant and spectator mode
- smoke test realtime propagation of join/vote/reveal/reset/mode-change events across multiple browser sessions/tabs

---

## Deliverables

When you implement this, the final result should include:

1. a new backend service: `Overflow.EstimationService`
2. updated local dev wiring in `Overflow.AppHost`
3. explicit Aspire registration for `estimation-svc`, its `estimationDb`, and local route wiring in `Overflow.AppHost/AppHost.cs`
4. updated solution/project wiring in `Overflow.slnx`
5. updated infra wiring in `terraform/` and `k8s/`
6. a new webapp Planning Poker section and room pages
7. side menu entry for Planning Poker
8. **raw WebSocket-based live room updates**
9. participant/spectator mode support
10. proper auth enforcement and moderator-only actions
11. guest join flow by room code/link
12. a short doc update if needed describing the new service and route

---

## Acceptance criteria

Consider the task done only if all of these are true:

- an authenticated user can create a planning poker room with title, code, and unique URL
- another authenticated user can join by room code/link
- a guest can join by room code/link with a display name
- any joined user can switch into spectator mode and back to participant mode
- spectators can watch but cannot vote
- spectators do not block reveal or count as pending voters
- participants can submit and change hidden votes
- moderator can reveal cards
- all viewers can see revealed votes, distribution, and numeric average after reveal
- moderator can reset the round and voting starts fresh
- moderator-only actions are enforced server-side
- archived rooms remain viewable as read-only
- the webapp shows a `Planning Poker` entry in the side menu
- the new service is registered in Aspire `Overflow.AppHost`, including `estimationDb` and local route wiring
- the feature works locally with Aspire + webapp
- live room state updates propagate in near real time via raw websocket communication
- the new service is wired into k8s and terraform manifests
- build/lint checks pass for touched code
