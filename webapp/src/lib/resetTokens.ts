// Simple token store (in production, use Redis or database)
// This is a temporary in-memory store for demo purposes

interface ResetToken {
    email: string;
    token: string;
    expiresAt: number;
}

const resetTokens = new Map<string, ResetToken>();

// Clean up expired tokens every hour
setInterval(() => {
    const now = Date.now();
    for (const [token, data] of resetTokens.entries()) {
        if (data.expiresAt < now) {
            resetTokens.delete(token);
        }
    }
}, 60 * 60 * 1000); // 1 hour

export function createResetToken(email: string): string {
    const token = generateSecureToken();
    const expiresAt = Date.now() + (15 * 60 * 1000); // 15 minutes

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

