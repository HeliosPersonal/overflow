import { NextRequest, NextResponse } from 'next/server';
import { KeycloakAdminClient, KeycloakAdminError } from '@/lib/keycloak-admin';
import logger from '@/lib/logger';

/**
 * POST /api/auth/signup
 *
 * Creates a new Keycloak user account with the provided credentials.
 * Uses the Keycloak Admin API (service account) to create the user.
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

        logger.info({ email }, 'User registered');

        return NextResponse.json({
            message: 'Account created successfully',
            email,
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
