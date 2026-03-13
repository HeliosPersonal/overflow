# Investigation: Participant Leave / Removal from Planning Poker Rooms

> **Implemented**: Strategy A (with presence tracking) — see "Implementation" section at the bottom.

## Problem Statement

Currently, participants remain in the room's participant list indefinitely. Three scenarios are broken:

1. **Page navigation / tab close** — user navigates away or closes the browser tab, but stays in participants forever.
2. **"Leave Room" button** — user clicks "Leave Room", gets redirected, but is never actually removed from participants.
3. **Inactivity** — user opens the room and walks away for hours; they remain listed as present.

---

## Root Cause Analysis

### 1. "Leave Room" button does NOT call the leave API

```tsx
// RoomClient.tsx, line 248
function handleLeave() {
    // Just navigate away — the WS close removes the participant server-side
    router.push('/planning-poker');
}
```

The button **only navigates away** — it relies entirely on the WebSocket disconnect handler to trigger server-side removal. It never calls `POST /estimation/rooms/{roomId}/leave`.

### 2. WebSocket disconnect DOES call `LeaveRoomAsync` — but it may not fire reliably

The server-side WebSocket endpoint (`WebSocketEndpoints.cs`, lines 93–101) has a `finally` block that:
1. Calls `broadcaster.RemoveConnection(roomId, participantId)` (removes the in-memory WS reference).
2. Creates a new DI scope and calls `leaveService.LeaveRoomAsync(roomId, participantId)` which **deletes the participant from DB + their votes**.

```csharp
// WebSocketEndpoints.cs — finally block
broadcaster.RemoveConnection(roomId, identity.ParticipantId);

using var scope = scopeFactory.CreateScope();
var leaveService = scope.ServiceProvider.GetRequiredService<EstimationRoomService>();
var leaveResult = await leaveService.LeaveRoomAsync(roomId, identity.ParticipantId);
```

**Why it fails:**

| Scenario | What happens | Result |
|---|---|---|
| **Client-side navigation** (`router.push`) | Next.js navigates without closing the WS explicitly; the `useEffect` cleanup calls `ws.close()`, which triggers the server `finally` block | **Should work** — but depends on the cleanup running before the browser tears down the page. SPA navigation should be fine; full page unload may race. |
| **Tab close / browser close** | The browser abruptly terminates the TCP connection. The server's `socket.ReceiveAsync` eventually throws `WebSocketException`. The `catch` block fires → `finally` runs. | **Works, but with delay** — the server relies on TCP keepalive / `KeepAliveInterval` (30s) to detect dead connections. The participant can appear "present" for up to 30 seconds. |
| **Network interruption** (laptop sleep, WiFi drop) | Same as above — TCP connection becomes stale. `KeepAliveInterval` (30s) will eventually detect it. | **Works after 30s–2min delay** depending on TCP/OS timeouts. |
| **User hits "Leave Room" → `router.push`** | The React `useEffect` cleanup fires `ws.close()`. Since `useRoomWebSocket` hook returns and `roomId` becomes `null`, the cleanup runs. The WS close triggers server-side leave. | **Should work** — but the issue is the race: if the component unmounts before the WS close handshake completes, the server may not receive the close frame cleanly. |

**Actual bug found:** The `useRoomWebSocket` hook properly closes the socket in its `useEffect` cleanup:

```typescript
// useRoomWebSocket.ts, line 92
return () => {
    if (wsRef.current) {
        wsRef.current.close();
        wsRef.current = null;
    }
};
```

But `handleLeave` just does `router.push('/planning-poker')`. In a Next.js SPA transition, the component unmounts → `useEffect` cleanup runs → `ws.close()` fires → server should get the close frame. **In theory this works.**

**The real problem:** After investigation, the WebSocket connection's `finally` block does fire and `LeaveRoomAsync` does delete the participant. **However**, the user might re-join the room on the next page load (the `bootstrap` `useEffect` in `RoomClient.tsx` auto-joins on mount). If the user refreshes or navigates back, they're automatically re-added.

Wait — that's the expected join flow. Let me re-examine...

**Actually, the likely root cause is:** The WebSocket is only opened when `joinedOnce` is `true`:

```tsx
const {room, status: wsStatus, updateRoom} = useRoomWebSocket(joinedOnce ? roomId : null);
```

If the bootstrap HTTP join succeeds but the WebSocket never connects (e.g., YARP doesn't proxy WS correctly, or the connection fails), the participant is in DB but has no WebSocket. When they leave, there's no WS to close, so `LeaveRoomAsync` is never called.

Also: **if the WebSocket connection encounters an error during the initial handshake** (before the receive loop), the `finally` block still runs and calls `LeaveRoomAsync`, which deletes them — but they were just joined via HTTP. There's a race condition between the HTTP join and the WS connect.

### 3. No inactivity timeout exists

There is no server-side mechanism to detect or remove inactive participants. The `ArchivedRoomCleanupService` only deletes archived rooms after 30 days — it doesn't touch active rooms with stale participants.

### 4. `isPresent` is always `true` (hardcoded)

```csharp
// RoomResponseMapper.cs, line 86
return new ParticipantResponse(
    // ...
    true // IsPresent — participants are only in the list if they're present (leave = delete)
);
```

The `isPresent` field is always `true` because the design assumes "leave = delete from DB". There's no concept of "present but disconnected" — participants either exist (present) or don't (gone).

---

## Available Strategies

### Strategy A: Fix the Existing "WS Disconnect = Delete" Approach

**What:** Keep the current design (leave = delete from DB) but fix the reliability of the WebSocket disconnect handler and add the explicit leave API call.

**Changes needed:**

**Frontend:**
- `handleLeave()`: Call `POST /estimation/rooms/{roomId}/leave` explicitly **before** navigating away.
- Add a `beforeunload` event listener that sends a `navigator.sendBeacon()` or synchronous leave request on tab close.
- Consider adding a `visibilitychange` listener as a backup signal.

**Backend:**
- No changes needed — `LeaveRoomAsync` already works correctly.
- Optionally add a `DELETE`-style leave endpoint that accepts `sendBeacon` (which only sends POST).

**Pros:**
- Minimal backend changes.
- Participants are truly removed — clean state.
- Works with existing `isPresent: true` hardcode.

**Cons:**
- `beforeunload` + `sendBeacon` is best-effort — not 100% reliable (mobile Safari, network drops).
- Doesn't solve the inactivity case (user leaves tab open but walks away).
- Network interruptions still leave stale participants until TCP timeout (30s–2min).
- If user closes laptop lid (sleep), participant stays until TCP times out.

---

### Strategy B: Presence Tracking via WebSocket Connection State

**What:** Instead of deleting participants on disconnect, track "online/offline" status. Participants persist in DB but have a `isConnected` / `isPresent` field derived from whether they have an active WebSocket connection.

**Changes needed:**

**Backend:**
- `RoomResponseMapper`: Set `isPresent` based on whether the participant has an active WebSocket connection in `WebSocketBroadcaster._connections`.
- Pass the set of connected participant IDs into the mapper.
- `LeaveRoomAsync` is still called on explicit leave (button) — this truly deletes the participant.
- WS disconnect: Only remove from `_connections`, do NOT call `LeaveRoomAsync`. The participant stays in DB as "offline".

**Frontend:**
- `handleLeave()`: Call `POST /estimation/rooms/{roomId}/leave` explicitly (this deletes from DB).
- UI shows offline participants greyed out or with an indicator.
- `beforeunload`: Optional — the WS close will mark them offline anyway.

**Pros:**
- Clean separation: "leave" (intentional) vs "disconnect" (unintentional).
- Participants who refresh or have temporary network issues don't lose their seat.
- `isPresent` becomes meaningful (currently hardcoded to `true`).
- No data loss on accidental disconnect.

**Cons:**
- Multi-pod complexity: `isPresent` is derived from in-memory WS connections on a specific pod. The mapper would need to know connections across ALL pods (requires Redis tracking or accepting per-pod-only accuracy).
- Offline participants clutter the participant list.
- Doesn't solve inactivity timeout by itself (need Strategy D too).
- More complex implementation.

---

### Strategy C: Heartbeat / Ping-Based Presence

**What:** Implement a client-to-server heartbeat. The client sends a periodic ping (e.g., every 15s) over WebSocket or HTTP. The server tracks `lastSeenAtUtc` per participant. A background service removes participants who haven't pinged within a threshold (e.g., 60s).

**Changes needed:**

**Backend:**
- Add `LastSeenAtUtc` column to `EstimationParticipant` (EF Core migration).
- WebSocket endpoint: Update `LastSeenAtUtc` on each received message (currently all client messages are ignored).
- Or: Add an HTTP heartbeat endpoint (`POST /estimation/rooms/{roomId}/heartbeat`).
- New `BackgroundService` (`StaleParticipantCleanupService`): Runs every 30s, removes participants whose `LastSeenAtUtc` is older than threshold.
- Update `LastSeenAtUtc` on join and on every WS message received.

**Frontend:**
- Send periodic ping messages over the WebSocket (e.g., every 15 seconds).
- Or: Call an HTTP heartbeat endpoint periodically.
- `beforeunload` / explicit leave: Optional improvements, but the heartbeat timeout handles everything.

**Pros:**
- Handles ALL cases: page close, navigation, network drops, inactivity, laptop sleep.
- Self-healing — stale participants are guaranteed to be cleaned up.
- No reliance on `beforeunload` or `sendBeacon` browser APIs.
- Works correctly in multi-pod (heartbeat is stored in DB, cleanup service reads from DB).

**Cons:**
- Adds DB writes every 15s per participant per room (can be optimized with in-memory tracking + periodic batch flush).
- Adds a background service.
- Slight delay before stale participants are removed (configurable, e.g., 60s).
- More DB load on rooms with many participants.

---

### Strategy D: Inactivity Timeout (Complementary)

**What:** A server-side background service that removes participants who have been inactive for a configurable duration (e.g., 2–4 hours). This is a safety net, not the primary mechanism.

**Changes needed:**

**Backend:**
- Reuse `LastSeenAtUtc` from Strategy C (or `JoinedAtUtc` / `UpdatedAtUtc` as a proxy).
- New or extended `BackgroundService`: Periodically scan for participants in non-archived rooms where `LastSeenAtUtc` (or last vote/action time) exceeds the inactivity threshold.
- Remove them via `LeaveRoomAsync` + broadcast updated state.

**Pros:**
- Catches any participants that slip through other mechanisms.
- Configurable threshold.
- Low overhead (runs infrequently, e.g., every 30 minutes).

**Cons:**
- By itself, not responsive enough for real-time UX (hours-scale, not seconds-scale).
- Needs a "last activity" timestamp — either `LastSeenAtUtc` (from heartbeat) or derived from vote/join timestamps.

---

### Strategy E: Hybrid — Explicit Leave + Heartbeat + Inactivity Timeout (Recommended)

**What:** Combine the best parts of Strategies A, C, and D for comprehensive coverage.

| Layer | Mechanism | Latency | Covers |
|---|---|---|---|
| **1. Explicit leave** | `handleLeave()` calls `POST /leave` before `router.push` | Instant | User clicks "Leave Room" |
| **2. `beforeunload`** | `navigator.sendBeacon` to leave endpoint | Instant (best-effort) | Tab close, browser close, external navigation |
| **3. WS disconnect** | Server `finally` block (existing) | 0–30s | SPA navigation, tab close fallback |
| **4. Heartbeat** | Client sends WS ping every 15s; server tracks `LastSeenAtUtc` | ~60s after disconnect | Network drops, laptop sleep, mobile backgrounding |
| **5. Inactivity sweep** | Background service removes participants idle > N hours | Hours | Zombie participants, edge cases |

**Changes needed:**

**Database:**
- Add `LastSeenAtUtc` (`DateTime?`) to `EstimationParticipant` entity + migration.

**Backend:**
- Update `LastSeenAtUtc` on WS message receive (in the receive loop where client messages are currently ignored).
- Update `LastSeenAtUtc` on join.
- New `StaleParticipantCleanupService` (BackgroundService):
  - Runs every 30s.
  - Finds participants in active rooms where `LastSeenAtUtc < DateTime.UtcNow - TimeSpan.FromSeconds(60)` AND participant has no active WS connection.
  - Calls `LeaveRoomAsync` for each + broadcasts.
- New `InactivityCleanupService` (or extend the above):
  - Runs every 30 min.
  - Finds participants in active rooms where `LastSeenAtUtc < DateTime.UtcNow - TimeSpan.FromHours(configurable)`.
  - Removes them regardless of WS connection state.
- Configuration via `appsettings.json`:
  - `ParticipantCleanup:HeartbeatTimeoutSeconds` (default: 60)
  - `ParticipantCleanup:InactivityTimeoutHours` (default: 4)
  - `ParticipantCleanup:CleanupIntervalSeconds` (default: 30)

**Frontend (`RoomClient.tsx` + `useRoomWebSocket.ts`):**
- `useRoomWebSocket`: Send a `"ping"` message every 15s over the WebSocket.
- `RoomClient.tsx` `handleLeave()`: Call `POST /estimation/rooms/{roomId}/leave` before navigating.
- Add `beforeunload` listener: `navigator.sendBeacon('/api/estimation/rooms/{roomId}/leave')`.
- Clean up `beforeunload` listener on unmount.

**Pros:**
- Covers every failure mode.
- Self-healing — no manual intervention needed.
- Responsive (explicit leave = instant; heartbeat timeout = ~60s; inactivity = hours).
- Multi-pod safe (heartbeat stored in DB; cleanup service reads from DB).
- Backwards compatible (existing `LeaveRoomAsync` logic unchanged).

**Cons:**
- Most complex to implement (but each piece is small and isolated).
- DB writes every 15s per participant (mitigated: only update if `LastSeenAtUtc` is >10s old to batch).
- Two new background services (can be combined into one).

---

## Strategy Comparison Matrix

| Criteria | A (Fix WS) | B (Presence Tracking) | C (Heartbeat) | D (Inactivity) | E (Hybrid) |
|---|---|---|---|---|---|
| Page close / navigate | ✅ Mostly | ✅ Yes | ✅ Yes (delayed) | ❌ Hours | ✅ Yes |
| "Leave Room" button | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No | ✅ Yes |
| Network drop | ⚠️ 30s–2m | ⚠️ 30s–2m | ✅ ~60s | ❌ Hours | ✅ ~60s |
| Laptop sleep | ⚠️ Variable | ⚠️ Variable | ✅ ~60s | ❌ Hours | ✅ ~60s |
| Long inactivity | ❌ No | ❌ No | ❌ No | ✅ Yes | ✅ Yes |
| Multi-pod safe | ✅ Yes | ⚠️ Complex | ✅ Yes | ✅ Yes | ✅ Yes |
| Implementation effort | Low | Medium | Medium | Low | Medium-High |
| DB writes overhead | None | None | Medium | None | Medium |
| Self-healing | ❌ No | ❌ No | ✅ Yes | ✅ Yes | ✅ Yes |
| Participant re-join after refresh | ❌ Lost | ✅ Preserved | ❌ Lost (if timeout) | ❌ Lost (if timeout) | ❌ Lost (if timeout) |

---

## Files That Would Be Modified (Strategy E)

### Backend
| File | Change |
|---|---|
| `Models/EstimationParticipant.cs` | Add `LastSeenAtUtc` property |
| `Data/EstimationDbContext.cs` | (migration auto-handles) |
| `Migrations/` | New migration for `LastSeenAtUtc` column |
| `Extensions/WebSocketEndpoints.cs` | Update `LastSeenAtUtc` on each received WS message |
| `Services/EstimationRoomService.cs` | Update `LastSeenAtUtc` on join; add method to remove stale participants |
| `Services/StaleParticipantCleanupService.cs` | **New file** — background service for heartbeat timeout + inactivity cleanup |
| `Options/ParticipantCleanupOptions.cs` | **New file** — IOptions for configurable timeouts |
| `Program.cs` | Register new services + options |
| `appsettings.json` | Add `ParticipantCleanup` section |

### Frontend
| File | Change |
|---|---|
| `webapp/src/lib/hooks/useRoomWebSocket.ts` | Add periodic ping (15s interval) |
| `webapp/src/app/(main)/planning-poker/[roomId]/RoomClient.tsx` | `handleLeave()` calls leave API; add `beforeunload` listener |

---

## Open Questions for Discussion

1. **Should disconnected participants be kept in the list as "offline"?** (Strategy B preserves them; Strategy E deletes them after timeout). This affects UX — do users want to see who was in the room even after they left?
2. **What heartbeat timeout feels right?** 60s is responsive but might flash-remove users on brief network hiccups. 120s is safer but slower.
3. **Should the moderator be exempted from inactivity cleanup?** (If the moderator goes AFK, the room becomes uncontrollable.)
4. **Should there be a "rejoin" concept?** Currently join is idempotent — if a removed participant navigates back, they're re-added. Is that acceptable?
5. **Should `navigator.sendBeacon` use a new dedicated endpoint** (since sendBeacon only sends POST with specific content types)?

