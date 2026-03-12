import { NextRequest, NextResponse } from 'next/server';
import {
    KeycloakAdminClient,
    KeycloakAdminError,
    ANONYMOUS_EMAIL_DOMAIN,
} from '@/lib/keycloak-admin';

/**
 * POST /api/auth/anonymous
 *
 * Creates a real Keycloak user with random credentials and returns them
 * so the client can immediately sign in via NextAuth Credentials provider.
 *
 * The user gets:
 *   - username & email: `anon_<random>@anonymous.overflow.local`
 *   - firstName: the display name the user chose
 *   - lastName: "Guest"
 *   - a long random password (never shown to the user)
 *
 * Anonymous users are detected elsewhere by their email domain
 * (see `isAnonymousEmail()` in `lib/keycloak-admin.ts`).
 *
 * Request body: { displayName: string }
 * Response:     { email: string, password: string, displayName: string }
 */
export async function POST(request: NextRequest) {
    try {
        const { displayName } = await request.json();

        if (!displayName || typeof displayName !== 'string' || !displayName.trim()) {
            return NextResponse.json({ error: 'Display name is required' }, { status: 400 });
        }

        const trimmedName = displayName.trim();

        // Generate random credentials (the user will never see these)
        const randomId = crypto.randomUUID().replace(/-/g, '').slice(0, 12);
        const placeholderEmail = `anon_${randomId}${ANONYMOUS_EMAIL_DOMAIN}`;
        const password = crypto.randomUUID() + crypto.randomUUID();

        // Create user in Keycloak via Admin API
        const kc = new KeycloakAdminClient();
        await kc.authenticate();

        await kc.createUser({
            username: placeholderEmail,
            email: placeholderEmail,
            firstName: trimmedName,
            lastName: 'Guest',
            enabled: true,
            emailVerified: true,       // Must be true — Keycloak blocks Direct Access Grant otherwise
            requiredActions: [],       // No pending actions — same reason
            credentials: [{ type: 'password', value: password, temporary: false }],
        });

        console.info('[GuestAuth] Anonymous user created:', placeholderEmail);

        return NextResponse.json({
            email: placeholderEmail,
            password,
            displayName: trimmedName,
        });
    } catch (error) {
        if (error instanceof KeycloakAdminError) {
            return NextResponse.json({ error: error.message }, { status: error.statusCode });
        }
        console.error('[GuestAuth] Unexpected error creating anonymous user:', error);
        return NextResponse.json({ error: 'An unexpected error occurred' }, { status: 500 });
    }
}
