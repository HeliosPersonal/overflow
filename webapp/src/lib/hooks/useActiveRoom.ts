/**
 * Tiny global store that tracks the planning-poker room the current user has
 * joined. This allows components outside the room page (e.g. the sign-out
 * button in UserMenu) to trigger a leave request *before* the session is
 * destroyed — otherwise the proxy can no longer attach a Bearer token and the
 * backend can't identify the participant.
 */

let activeRoomId: string | null = null;

export function setActiveRoom(roomId: string | null) {
    activeRoomId = roomId;
}

export function getActiveRoom(): string | null {
    return activeRoomId;
}

