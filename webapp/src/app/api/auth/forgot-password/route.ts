import { NextRequest, NextResponse } from 'next/server';
import { KeycloakAdminClient } from '@/lib/keycloak-admin';

/**
 * POST /api/auth/forgot-password
 *
 * Triggers a password reset email via Keycloak for the given email.
 * Always returns success to prevent email enumeration.
 *
 * Request body: { email: string }
 */
export async function POST(request: NextRequest) {
    const SUCCESS_MSG = 'If an account exists, a password reset email has been sent.';

    try {
        const { email } = await request.json();

        if (!email) {
            return NextResponse.json({ error: 'Email is required' }, { status: 400 });
        }

        const kc = new KeycloakAdminClient();
        await kc.authenticate();

        // Find user by email
        const users = await kc.findUsersByEmail(email);

        if (users.length > 0) {
            // Execute password reset action — Keycloak sends the email
            try {
                await kc.executeActions(users[0].id, ['UPDATE_PASSWORD']);
            } catch (error) {
                console.error('[Auth] Failed to send password reset email:', error);
            }
        }

        // Always return success to prevent email enumeration
        return NextResponse.json({ message: SUCCESS_MSG });
    } catch (error) {
        console.error('[Auth] Forgot password error:', error);
        return NextResponse.json(
            { error: 'An unexpected error occurred. Please try again.' },
            { status: 500 },
        );
    }
}
