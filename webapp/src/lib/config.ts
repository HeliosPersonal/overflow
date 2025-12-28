// Check if we're in a build environment (during next build)
const isBuild = process.env.NEXT_PHASE === 'phase-production-build';

function getEnv(name: keyof NodeJS.ProcessEnv, fallback: string = ''): string {
    const value = process.env[name];
    
    // During build, return fallback to prevent errors
    // The actual values will be available at runtime
    if (isBuild && !value) {
        return fallback;
    }
    
    // At runtime, throw error if missing (strict validation)
    if (!value) {
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
};

export const apiConfig = {
    get baseUrl() { return getEnv('API_URL', 'https://placeholder.local'); },
};

export const cloudinaryConfig = {
    get cloudName() { return getEnv('NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME', 'placeholder'); },
    get apiKey() { return getEnv('CLOUDINARY_API_KEY', 'placeholder-key'); },
    get apiSecret() { return getEnv('CLOUDINARY_API_SECRET', 'placeholder-secret'); },
};
