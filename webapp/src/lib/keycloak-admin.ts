import {authConfig} from '@/lib/config';
import { createLogger } from '@/lib/logger';

const logger = createLogger('keycloak-admin');

/**
 * Domain used for anonymous (guest) user placeholder emails in Keycloak.
 * Any user whose email ends with this domain is treated as an anonymous guest.
 *
 * Used to detect anonymous users during sign-in (auth.ts), upgrade eligibility
 * (upgrade/route.ts), and session display (UserMenu, ProfileDetailed).
 */
export const ANONYMOUS_EMAIL_DOMAIN = '@anonymous.overflow.local';

/**
 * Check whether an email address belongs to an anonymous guest account.
 */
export function isAnonymousEmail(email: string | null | undefined): boolean {
    return !!email?.endsWith(ANONYMOUS_EMAIL_DOMAIN);
}

/**
 * Parse the Keycloak issuer URL into base URL and realm name.
 *
 * e.g. "http://localhost:6001/realms/overflow"
 *   → { baseUrl: "http://localhost:6001", realmName: "overflow" }
 */
function parseKeycloakIssuer(): { baseUrl: string; realmName: string } {
    const issuer = authConfig.kcInternal;
    const parts = issuer.split('/realms/');
    return {
        baseUrl: parts[0],
        realmName: parts[1] || 'overflow',
    };
}

/**
 * Low-level helper for Keycloak Admin REST API calls.
 *
 * Handles admin token acquisition and provides typed methods for
 * common operations: creating users, finding users, updating users,
 * and resetting passwords.
 *
 * Used by:
 *   - POST /api/auth/anonymous        → createUser()
 *   - POST /api/auth/signup           → createUser()
 *   - POST /api/auth/upgrade          → getUserById(), updateUser(), resetPassword()
 *   - POST /api/auth/forgot-password  → findUsersByEmail(), executeActions()
 *   - POST /api/auth/reset-password   → findUsersByEmail(), resetPassword()
 */
export class KeycloakAdminClient {
    private adminToken: string | null = null;
    private readonly baseUrl: string;
    private readonly realmName: string;

    constructor() {
        const {baseUrl, realmName} = parseKeycloakIssuer();
        this.baseUrl = baseUrl;
        this.realmName = realmName;
    }

    /**
     * Acquire an admin access token via client_credentials grant.
     * Caches the token for the lifetime of this instance (use one instance per request).
     */
    async authenticate(): Promise<void> {
        const tokenUrl = `${authConfig.kcInternal}/protocol/openid-connect/token`;

        const response = await fetch(tokenUrl, {
            method: 'POST',
            headers: {'Content-Type': 'application/x-www-form-urlencoded'},
            body: new URLSearchParams({
                grant_type: 'client_credentials',
                client_id: authConfig.kcAdminClientId,
                client_secret: authConfig.kcAdminClientSecret,
            }),
        });

        if (!response.ok) {
            const body = await response.text();
            logger.error({ status: response.status, body }, 'Failed to acquire admin token');
            throw new KeycloakAdminError('Failed to acquire admin token', 503);
        }

        const data = await response.json();
        this.adminToken = data.access_token;
    }

    /** Build the admin API URL for a given path segment. */
    private adminUrl(path: string): string {
        return `${this.baseUrl}/admin/realms/${this.realmName}${path}`;
    }

    /** Authorization header using the cached admin token. */
    private get authHeader(): Record<string, string> {
        if (!this.adminToken) throw new Error('KeycloakAdminClient: call authenticate() first');
        return {Authorization: `Bearer ${this.adminToken}`};
    }
    
    // ── User operations ───────────────────────────────────────────────

    /**
     * Create a new user in Keycloak.
     * @returns The Keycloak user ID (from the Location header).
     */
    async createUser(user: KeycloakCreateUserPayload): Promise<string> {
        const response = await fetch(this.adminUrl('/users'), {
            method: 'POST',
            headers: {'Content-Type': 'application/json', ...this.authHeader},
            body: JSON.stringify(user),
        });

        if (!response.ok) {
            const body = await response.text();
            logger.error({ status: response.status, body }, 'Failed to create user');
            throw new KeycloakAdminError(`Failed to create user: ${body}`, response.status);
        }

        // Keycloak returns the user ID in the Location header
        const location = response.headers.get('Location') || '';
        return location.split('/').pop() || '';
    }

    /** Fetch a user by their Keycloak user ID. */
    async getUserById(userId: string): Promise<KeycloakUser> {
        const response = await fetch(this.adminUrl(`/users/${userId}`), {
            headers: this.authHeader,
        });

        if (!response.ok) {
            const body = await response.text();
            logger.error({ status: response.status, body }, 'Failed to fetch user');
            throw new KeycloakAdminError('User not found', response.status);
        }

        return response.json();
    }

    /** Search for users by exact email match. */
    async findUsersByEmail(email: string): Promise<KeycloakUser[]> {
        const response = await fetch(
            this.adminUrl(`/users?email=${encodeURIComponent(email)}&exact=true`),
            {headers: this.authHeader},
        );

        if (!response.ok) return [];
        return response.json();
    }

    /** Update an existing user (full PUT replacement). */
    async updateUser(userId: string, user: Partial<KeycloakUser>): Promise<void> {
        const response = await fetch(this.adminUrl(`/users/${userId}`), {
            method: 'PUT',
            headers: {'Content-Type': 'application/json', ...this.authHeader},
            body: JSON.stringify(user),
        });

        if (!response.ok) {
            const body = await response.text();
            logger.error({ status: response.status, body }, 'Failed to update user');
            throw new KeycloakAdminError(
                response.status === 409 ? 'Email already taken' : `Failed to update user: ${body}`,
                response.status,
            );
        }
    }

    /** Set a user's password (non-temporary). */
    async resetPassword(userId: string, newPassword: string): Promise<void> {
        const response = await fetch(this.adminUrl(`/users/${userId}/reset-password`), {
            method: 'PUT',
            headers: {'Content-Type': 'application/json', ...this.authHeader},
            body: JSON.stringify({type: 'password', value: newPassword, temporary: false}),
        });

        if (!response.ok) {
            const body = await response.text();
            logger.error({ status: response.status, body }, 'Failed to reset password');
            throw new KeycloakAdminError(`Failed to reset password: ${body}`, response.status);
        }
    }

    /** Trigger required actions email (e.g. UPDATE_PASSWORD) for a user. */
    async executeActions(userId: string, actions: string[]): Promise<void> {
        const response = await fetch(this.adminUrl(`/users/${userId}/execute-actions-email`), {
            method: 'PUT',
            headers: {'Content-Type': 'application/json', ...this.authHeader},
            body: JSON.stringify(actions),
        });

        if (!response.ok) {
            const body = await response.text();
            logger.error({ status: response.status, body }, 'Failed to execute actions');
            throw new KeycloakAdminError(`Failed to execute actions: ${body}`, response.status);
        }
    }
}

// ── Types ─────────────────────────────────────────────────────────────

export interface KeycloakCreateUserPayload {
    username: string;
    email: string;
    firstName: string;
    lastName: string;
    enabled: boolean;
    emailVerified: boolean;
    requiredActions: string[];
    credentials: { type: string; value: string; temporary: boolean }[];
}

export interface KeycloakUser {
    id: string;
    username: string;
    email?: string;
    firstName?: string;
    lastName?: string;
    enabled?: boolean;
    emailVerified?: boolean;
    [key: string]: unknown; // Keycloak returns many additional fields
}

/**
 * Error thrown by KeycloakAdminClient operations.
 * `statusCode` maps to the appropriate HTTP status for API route responses.
 */
export class KeycloakAdminError extends Error {
    constructor(message: string, public readonly statusCode: number) {
        super(message);
        this.name = 'KeycloakAdminError';
    }
}



