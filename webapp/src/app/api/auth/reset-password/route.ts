import { NextRequest, NextResponse } from 'next/server';
import { KeycloakAdminClient, KeycloakAdminError } from '@/lib/keycloak-admin';
import { verifyResetToken, consumeResetToken } from '@/lib/resetTokens';
import logger from '@/lib/logger';

/**
 * POST /api/auth/reset-password
 *
 * Resets a user's password using a previously issued reset token.
 * The token is consumed after use so it cannot be reused.
 *
 * Request body: { token: string, email: string, password: string }
 */
export async function POST(request: NextRequest) {
    try {
        const { token, email, password } = await request.json();

        if (!token || !email || !password) {
            return NextResponse.json({ error: 'Missing required fields' }, { status: 400 });
        }

        // Verify reset token
        const tokenData = verifyResetToken(token);
        if (!tokenData.valid || tokenData.email !== email) {
            return NextResponse.json({ error: 'Invalid or expired reset token' }, { status: 400 });
        }

        const kc = new KeycloakAdminClient();
        await kc.authenticate();

        // Find user by email
        const users = await kc.findUsersByEmail(email);
        if (users.length === 0) {
            return NextResponse.json({ error: 'User not found' }, { status: 404 });
        }

        // Reset password
        await kc.resetPassword(users[0].id, password);

        // Consume the token so it can't be reused
        consumeResetToken(token);

        return NextResponse.json({ message: 'Password reset successfully' });
    } catch (error) {
        if (error instanceof KeycloakAdminError) {
            return NextResponse.json({ error: 'Failed to reset password' }, { status: error.statusCode });
        }
        logger.error({ err: error }, 'Reset password error');
        return NextResponse.json({ error: 'An unexpected error occurred' }, { status: 500 });
    }
}
