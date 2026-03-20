const isBuild = process.env.NEXT_PHASE === 'phase-production-build';
const isServer = typeof window === 'undefined';

function getEnv(name: keyof NodeJS.ProcessEnv, fallback: string = ''): string {
    const value = process.env[name];
    
    // During build, return fallback — actual values are available at runtime
    if (isBuild && !value) return fallback;
    
    if (!value) {
        const context = isServer ? 'server' : 'client';
        console.error(`[Config] Missing required env var: ${name} (${context}-side)`);
        throw new Error(`Could not find env: ${name}`);
    }
    
    return value;
}

export const authConfig = {
    get kcIssuer() { return getEnv('AUTH_KEYCLOAK_ISSUER', 'https://placeholder.local'); },
    get kcSecret() { return getEnv('NEXTAUTH_KEYCLOAK_CLIENT_SECRET', 'placeholder-secret'); },
    get kcClientId() { return getEnv('AUTH_KEYCLOAK_ID', 'placeholder-client'); },
    get kcInternal() { return getEnv('AUTH_KEYCLOAK_ISSUER_INTERNAL', 'https://placeholder.local'); },
    get secret() { return getEnv('AUTH_SECRET', 'placeholder-secret'); },
    get authUrl() { return getEnv('AUTH_URL', 'https://placeholder.local'); },
    get kcAdminClientId() { return getEnv('KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID', 'placeholder-admin-client'); },
    get kcAdminClientSecret() { return getEnv('KEYCLOAK_OPTIONS_ADMIN_CLIENT_SECRET', 'placeholder-admin-secret'); },
};

export const apiConfig = {
    get baseUrl() { return getEnv('API_URL', 'https://placeholder.local'); },
    get notificationApiKey() { return getEnv('NOTIFICATION_INTERNAL_API_KEY', 'placeholder-key'); },
};

