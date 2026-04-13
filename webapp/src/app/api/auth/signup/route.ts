import { NextRequest, NextResponse } from 'next/server';
import { KeycloakAdminClient, KeycloakAdminError } from '@/lib/keycloak-admin';
import { apiConfig, authConfig } from '@/lib/config';
import { createResetToken, EMAIL_VERIFICATION_EXPIRY_MS } from '@/lib/resetTokens';
import { createLogger } from '@/lib/logger';

const logger = createLogger('auth-signup');

/**
 * POST /api/auth/signup
 *
 * Creates a new Keycloak user account with the provided credentials.
 * Uses the Keycloak Admin API (service account) to create the user.
 * Sends a verification email — user must verify before they can sign in.
 *
 * Request body: { email: string, firstName: string, lastName: string, password: string }
 */
export async function POST(request: NextRequest) {
    try {
        const { email, firstName, lastName, password } = await request.json();

        if (!email || !firstName || !lastName || !password) {
            return NextResponse.json({ error: 'All fields are required' }, { status: 400 });
        }

        const kc = new KeycloakAdminClient();
        await kc.authenticate();

        // Note: registrationEmailAsUsername is enabled in Keycloak,
        // so we pass email as username for consistency.
        await kc.createUser({
            username: email,
            email,
            firstName,
            lastName,
            enabled: true,
            emailVerified: false,
            requiredActions: [],
            credentials: [{ type: 'password', value: password, temporary: false }],
        });

        // Send verification email via NotificationService
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

        logger.info({ email }, 'User registered (pending email verification)');

        return NextResponse.json({
            message: 'Account created successfully. Please check your email to verify your address.',
            email,
            requiresVerification: true,
        }, { status: 201 });
    } catch (error) {
        if (error instanceof KeycloakAdminError) {
            if (error.statusCode === 409) {
                return NextResponse.json(
                    { error: 'An account with this email already exists' },
                    { status: 409 },
                );
            }
            if (error.statusCode === 403) {
                logger.error('Signup permission denied — service account needs manage-users role');
                return NextResponse.json(
                    { error: 'Registration is temporarily unavailable. Please try again later.' },
                    { status: 503 },
                );
            }
            return NextResponse.json({ error: 'Failed to create user account' }, { status: error.statusCode });
        }
        logger.error({ err: error }, 'Signup error');
        return NextResponse.json(
            { error: 'An unexpected error occurred. Please try again.' },
            { status: 500 },
        );
    }
}
