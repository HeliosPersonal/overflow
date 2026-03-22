import { NextRequest, NextResponse } from 'next/server';
import { KeycloakAdminClient, KeycloakAdminError } from '@/lib/keycloak-admin';
import { verifyResetToken, consumeResetToken } from '@/lib/resetTokens';
import logger from '@/lib/logger';

/**
 * POST /api/auth/verify-email
 *
 * Verifies a user's email address using a token sent during account upgrade.
 * Sets emailVerified=true in Keycloak so the user can sign in with Direct Access Grant.
 *
 * Request body: { token: string, email: string }
 */
export async function POST(request: NextRequest) {
    try {
        const { token, email } = await request.json();

        if (!token || !email) {
            return NextResponse.json({ error: 'Missing required fields' }, { status: 400 });
        }

        // Validate the token
        const tokenData = verifyResetToken(token);
        if (!tokenData.valid || tokenData.email !== email) {
            return NextResponse.json({ error: 'Invalid or expired verification link' }, { status: 400 });
        }

        const kc = new KeycloakAdminClient();
        await kc.authenticate();

        // Find user by email
        const users = await kc.findUsersByEmail(email);
        if (users.length === 0) {
            return NextResponse.json({ error: 'User not found' }, { status: 404 });
        }

        const user = users[0];

        if (user.emailVerified) {
            // Already verified — consume the token and return success
            consumeResetToken(token);
            return NextResponse.json({ message: 'Email already verified' });
        }

        // Set emailVerified=true in Keycloak
        await kc.updateUser(user.id, {
            ...user,
            emailVerified: true,
        });

        // Consume the token so it can't be reused
        consumeResetToken(token);

        logger.info({ email }, 'Email verified for user');

        return NextResponse.json({ message: 'Email verified successfully' });
    } catch (error) {
        if (error instanceof KeycloakAdminError) {
            return NextResponse.json({ error: 'Failed to verify email' }, { status: error.statusCode });
        }
        logger.error({ err: error }, 'Verify email error');
        return NextResponse.json({ error: 'An unexpected error occurred' }, { status: 500 });
    }
}

