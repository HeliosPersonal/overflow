import { NextRequest, NextResponse } from 'next/server';
import { auth } from '@/auth';
import { apiConfig, authConfig } from '@/lib/config';
import {
    KeycloakAdminClient,
    KeycloakAdminError,
    isAnonymousEmail,
} from '@/lib/keycloak-admin';
import { createResetToken, EMAIL_VERIFICATION_EXPIRY_MS } from '@/lib/resetTokens';
import logger from '@/lib/logger';

/**
 * POST /api/auth/upgrade
 *
 * Upgrades an anonymous (guest) Keycloak account to a full account.
 *
 * Steps:
 *   1. Verify the caller is authenticated and is an anonymous user.
 *   2. Check that the desired email isn't already taken.
 *   3. Update the Keycloak user: set real email, username, and name.
 *      Set emailVerified=false — the user must verify via email link.
 *   4. Set the new password.
 *   5. Update display name in the ProfileService.
 *   6. Send a verification email via NotificationService.
 *
 * After this, the user must click the verification link before they can
 * sign in with the new credentials. The verify-email API route sets
 * emailVerified=true in Keycloak.
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

        // 3. Update Keycloak user: replace placeholder email with real one.
        //    emailVerified=false — user must verify via email link before sign-in.
        await kc.updateUser(userId, {
            ...kcUser,
            username: email,
            email,
            firstName: firstName || kcUser.firstName,
            lastName: lastName || kcUser.lastName || 'User',
            emailVerified: false,
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
            logger.warn({ err: profileError }, 'Profile display name update failed (non-fatal)');
        }

        // 6. Send verification email via NotificationService
        try {
            const token = createResetToken(email, EMAIL_VERIFICATION_EXPIRY_MS);
            const verifyUrl = `${authConfig.authUrl}/verify-email?token=${token}&email=${encodeURIComponent(email)}`;
            const appEnv = process.env.APP_ENV || 'production';
            const appName = appEnv === 'staging' ? 'Overflow Staging' : 'Overflow';

            await fetch(`${apiConfig.baseUrl}/notifications/send`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Api-Key': apiConfig.notificationApiKey,
                },
                body: JSON.stringify({
                    channel: 'Email',
                    recipient: email,
                    template: 'VerifyEmail',
                    parameters: { verifyUrl, appName },
                }),
            });
        } catch (emailError) {
            logger.error({ err: emailError }, 'Failed to send verification email');
        }

        logger.info({ from: kcUser.email, to: email }, 'Account upgraded (pending email verification)');

        return NextResponse.json({
            message: 'Account upgraded. Please check your email to verify your address.',
            requiresVerification: true,
        });
    } catch (error) {
        if (error instanceof KeycloakAdminError) {
            const status = error.statusCode === 409 ? 409 : error.statusCode >= 500 ? 500 : error.statusCode;
            return NextResponse.json({ error: error.message }, { status });
        }
        logger.error({ err: error }, 'Unexpected error upgrading account');
        return NextResponse.json({ error: 'An unexpected error occurred' }, { status: 500 });
    }
}
