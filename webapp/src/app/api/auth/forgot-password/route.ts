import { NextRequest, NextResponse } from 'next/server';
import { KeycloakAdminClient } from '@/lib/keycloak-admin';
import { createResetToken } from '@/lib/resetTokens';
import { apiConfig, authConfig } from '@/lib/config';
import logger from '@/lib/logger';

/**
 * POST /api/auth/forgot-password
 *
 * Generates a password-reset token and sends a notification request
 * to the NotificationService (via YARP gateway → RabbitMQ → Mailgun).
 *
 * The NotificationService resolves the "password-reset" template and
 * delivers the email. The token is verified + consumed by
 * POST /api/auth/reset-password, which sets the new password via
 * the Keycloak Admin API — the user never sees Keycloak UI.
 *
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
            try {
                // Generate our own reset token (15-minute expiry)
                const token = createResetToken(email);

                // Build the link to our custom reset-password page
                const resetUrl = `${authConfig.authUrl}/reset-password?token=${token}&email=${encodeURIComponent(email)}`;

                // Determine app display name from environment
                const appEnv = process.env.APP_ENV || 'production';
                const appName = appEnv === 'staging' ? 'Overflow Staging' : 'Overflow';

                // Send via NotificationService — authenticated with internal API key
                // (no user JWT is available in forgot-password flow)
                const response = await fetch(`${apiConfig.baseUrl}/notifications/send`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Api-Key': apiConfig.notificationApiKey,
                    },
                    body: JSON.stringify({
                        channel: 'Email',
                        recipient: email,
                        template: 'PasswordReset',
                        parameters: { resetUrl, appName },
                    }),
                });

                if (!response.ok) {
                    const body = await response.text();
                    logger.error({ status: response.status, body }, 'NotificationService error');
                }
            } catch (error) {
                logger.error({ err: error }, 'Failed to send password reset email');
            }
        }

        // Always return success to prevent email enumeration
        return NextResponse.json({ message: SUCCESS_MSG });
    } catch (error) {
        logger.error({ err: error }, 'Forgot password error');
        return NextResponse.json(
            { error: 'An unexpected error occurred. Please try again.' },
            { status: 500 },
        );
    }
}
