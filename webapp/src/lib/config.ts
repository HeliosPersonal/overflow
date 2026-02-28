// Check if we're in a build environment (during next build)
const isBuild = process.env.NEXT_PHASE === 'phase-production-build';
const isServer = typeof window === 'undefined';

function getEnv(name: keyof NodeJS.ProcessEnv, fallback: string = ''): string {
    const value = process.env[name];
    
    // During build, return fallback to prevent errors
    // The actual values will be available at runtime
    if (isBuild && !value) {
        console.log(`⚙️  [Config] Build phase - using fallback for ${name}`);
        return fallback;
    }
    
    // At runtime, throw an error if missing (strict validation)
    if (!value) {
        const context = isServer ? 'server' : 'client';
        console.error(`❌ [Config] Missing required environment variable: ${name} (${context}-side)`);
        throw new Error(`Could not find env: ${name}`);
    }
    
    return value;
}

export const authConfig = {
    get kcIssuer() { return getEnv('AUTH_KEYCLOAK_ISSUER', 'https://placeholder.local'); },
    get kcSecret() { return getEnv('AUTH_KEYCLOAK_SECRET', 'placeholder-secret'); },
    get kcClientId() { return getEnv('AUTH_KEYCLOAK_ID', 'placeholder-client'); },
    get kcInternal() { return getEnv('AUTH_KEYCLOAK_ISSUER_INTERNAL', 'https://placeholder.local'); },
    get secret() { return getEnv('AUTH_SECRET', 'placeholder-secret'); },
    get authUrl() { return getEnv('AUTH_URL', 'https://placeholder.local'); },
    get kcAdminClientId() { return getEnv('KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID', 'placeholder-admin-client'); },
    get kcAdminClientSecret() { return getEnv('KEYCLOAK_OPTIONS_ADMIN_CLIENT_SECRET', 'placeholder-admin-secret'); },
};

export const apiConfig = {
    get baseUrl() { return getEnv('API_URL', 'https://placeholder.local'); },
};

// TODO: Remove Cloudinary - commented out for now
/*
export const cloudinaryConfig = {
    get cloudName() { return getEnv('NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME', 'placeholder'); },
    get apiKey() { return getEnv('CLOUDINARY_API_KEY', 'placeholder-key'); },
    get apiSecret() { return getEnv('CLOUDINARY_API_SECRET', 'placeholder-secret'); },
};
*/
