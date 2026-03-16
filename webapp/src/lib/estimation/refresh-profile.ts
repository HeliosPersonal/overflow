/**
 * Notifies the EstimationService that the current user's profile has changed.
 * Evicts the cached profile and pushes the latest display name + avatar to every
 * room the user participates in, triggering immediate WebSocket broadcasts.
 *
 * Fire-and-forget — failures are logged but don't block the caller.
 */
export async function refreshEstimationProfile(): Promise<void> {
    try {
        await fetch('/api/estimation/refresh-profile', { method: 'POST' });
    } catch {
        // Best-effort: if estimation service is unreachable the rooms will
        // pick up the changes on next join (existing behaviour).
    }
}

