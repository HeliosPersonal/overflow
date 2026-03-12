import { NextRequest, NextResponse } from 'next/server';
import { auth } from '@/auth';
import { apiConfig } from '@/lib/config';
import {
    KeycloakAdminClient,
    KeycloakAdminError,
    isAnonymousEmail,
} from '@/lib/keycloak-admin';

/**
 * POST /api/auth/upgrade
 *
 * Upgrades an anonymous (guest) Keycloak account to a full account.
 *
 * Steps:
 *   1. Verify the caller is authenticated and is an anonymous user.
 *   2. Check that the desired email isn't already taken.
 *   3. Update the Keycloak user: set real email, username, and name.
 *   4. Set the new password.
 *   5. Update display name in the ProfileService.
 *
 * After this, the user can sign in with their chosen email + password,
 * and `isAnonymousEmail()` will return false for their new email.
 *
 * Request body: { email: string, password: string, firstName?: string, lastName?: string }
 */
export async function POST(request: NextRequest) {
    try {
        const session = await auth();
        if (!session?.user?.id) {
            return NextResponse.json({ error: 'Not authenticated' }, { status: 401 });
        }

        const { email, password, firstName, lastName } = await request.json();

        if (!email || !password) {
            return NextResponse.json({ error: 'Email and password are required' }, { status: 400 });
        }

        const userId = session.user.id;
        const kc = new KeycloakAdminClient();
        await kc.authenticate();

        // 1. Fetch current user and verify they are anonymous
        const kcUser = await kc.getUserById(userId);

        if (!isAnonymousEmail(kcUser.email)) {
            return NextResponse.json({ error: 'This account is already registered' }, { status: 400 });
        }

        // 2. Check email uniqueness
        const existingUsers = await kc.findUsersByEmail(email);
        if (existingUsers.length > 0 && existingUsers[0].id !== userId) {
            return NextResponse.json({ error: 'An account with this email already exists' }, { status: 409 });
        }

        // 3. Update Keycloak user: replace placeholder email with real one
        await kc.updateUser(userId, {
            ...kcUser,
            username: email,
            email,
            firstName: firstName || kcUser.firstName,
            lastName: lastName || kcUser.lastName || 'User',
            emailVerified: true,  // Must stay true — Keycloak blocks Direct Access Grant otherwise
        });

        // 4. Set the user's new password
        await kc.resetPassword(userId, password);

        // 5. Update display name in ProfileService (non-fatal if it fails)
        const newDisplayName = [firstName, lastName].filter(Boolean).join(' ') || firstName || email;
        try {
            await fetch(`${apiConfig.baseUrl}/profiles/edit`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${session.accessToken}`,
                },
                body: JSON.stringify({ displayName: newDisplayName }),
            });
        } catch (profileError) {
            console.warn('[GuestAuth] Profile display name update failed (non-fatal):', profileError);
        }

        console.info('[GuestAuth] Account upgraded:', kcUser.email, '→', email);

        return NextResponse.json({ message: 'Account upgraded successfully' });
    } catch (error) {
        if (error instanceof KeycloakAdminError) {
            const status = error.statusCode === 409 ? 409 : error.statusCode >= 500 ? 500 : error.statusCode;
            return NextResponse.json({ error: error.message }, { status });
        }
        console.error('[GuestAuth] Unexpected error upgrading account:', error);
        return NextResponse.json({ error: 'An unexpected error occurred' }, { status: 500 });
    }
}
