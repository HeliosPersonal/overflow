import { signIn } from 'next-auth/react';

/**
 * Create an anonymous Keycloak user and sign in via NextAuth in one step.
 *
 * This is the client-side counterpart of `POST /api/auth/anonymous`.
 * It creates a throwaway Keycloak account with the given display name,
 * then immediately signs in so the caller has a real authenticated session.
 *
 * Used by:
 *   - AuthGatePage       ("Continue as Guest" button)
 *   - RoomClient         (guest joining a planning poker room)
 *   - PlanningPokerLanding (guest creating a planning poker room)
 *
 * @returns `{ ok: true }` on success, `{ ok: false, error: string }` on failure.
 */
export async function createGuestAndSignIn(
    displayName: string,
    avatarUrl?: string,
): Promise<{ ok: true } | { ok: false; error: string }> {
    // Step 1 — Create anonymous Keycloak user (server-side)
    const createResponse = await fetch('/api/auth/anonymous', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: displayName.trim(), avatarUrl }),
    });

    if (!createResponse.ok) {
        const body = await createResponse.json().catch(() => null);
        return { ok: false, error: body?.error || 'Failed to create guest account' };
    }

    const { email, password } = await createResponse.json();

    // Step 2 — Sign in with the generated credentials
    const signInResult = await signIn('credentials', {
        email,
        password,
        redirect: false,
    });

    if (!signInResult?.ok) {
        return { ok: false, error: 'Failed to sign in. Please try again.' };
    }

    // Step 3 — Save avatar to the profile (must complete before page reload)
    if (avatarUrl) {
        try {
            await fetch('/api/profile/avatar', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ avatarUrl }),
            });
        } catch { /* best-effort */ }
    }

    return { ok: true };
}

