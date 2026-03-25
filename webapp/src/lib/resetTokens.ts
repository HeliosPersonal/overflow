// Simple token store (in production, use Redis or database)
// This is a temporary in-memory store for demo purposes

/** 15 minutes — default for password-reset tokens. */
export const PASSWORD_RESET_EXPIRY_MS = 15 * 60 * 1000;

/** 1 hour — used for email-verification tokens (account upgrade). */
export const EMAIL_VERIFICATION_EXPIRY_MS = 60 * 60 * 1000;

const CLEANUP_INTERVAL_MS = 60 * 60 * 1000;

interface ResetToken {
    email: string;
    token: string;
    expiresAt: number;
}

const resetTokens = new Map<string, ResetToken>();

// Clean up expired tokens periodically
setInterval(() => {
    const now = Date.now();
    for (const [token, data] of resetTokens.entries()) {
        if (data.expiresAt < now) {
            resetTokens.delete(token);
        }
    }
}, CLEANUP_INTERVAL_MS);

export function createResetToken(email: string, expiresInMs: number = PASSWORD_RESET_EXPIRY_MS): string {
    const token = generateSecureToken();
    const expiresAt = Date.now() + expiresInMs;

    resetTokens.set(token, {
        email,
        token,
        expiresAt,
    });

    return token;
}

export function verifyResetToken(token: string): { valid: boolean; email?: string } {
    const data = resetTokens.get(token);

    if (!data) {
        return { valid: false };
    }

    if (data.expiresAt < Date.now()) {
        resetTokens.delete(token);
        return { valid: false };
    }

    return { valid: true, email: data.email };
}

export function consumeResetToken(token: string): void {
    resetTokens.delete(token);
}

function generateSecureToken(): string {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
}

