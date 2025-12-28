/**
 * Next.js Instrumentation
 * This file is executed before the Next.js server starts
 *
 * Loads configuration:
 * - Development: Only from .env files
 * - Staging/Production: From .env files + Infisical secrets at runtime
 */

export async function register() {
    console.log('📦 Instrumentation: register() called', {
        NEXT_RUNTIME: process.env.NEXT_RUNTIME,
        NEXT_PHASE: process.env.NEXT_PHASE,
        NODE_ENV: process.env.NODE_ENV,
        APP_ENV: process.env.APP_ENV,
    });

    // Only run in Node.js runtime (not Edge runtime)
    if (process.env.NEXT_RUNTIME === 'nodejs') {
        // Skip during build phase - we only want to load secrets at runtime
        const isBuilding = process.env.NEXT_PHASE === 'phase-production-build';

        if (isBuilding) {
            console.log('📦 Instrumentation: Skipped (build phase)');
            return;
        }

        // At runtime, load configuration
        // In development: loads from .env files only
        // In staging/production: loads from .env files + Infisical
        try {
            console.log('📦 Instrumentation: Loading configuration...');
            const {loadConfiguration} = await import('../lib/infisical');
            await loadConfiguration();
            console.log('✅ Instrumentation: Configuration loaded successfully');
        } catch (error) {
            console.error('❌ Instrumentation: Failed to load configuration:', error);
            // Don't throw - allow app to start with whatever env vars are available
        }
    } else {
        console.log('📦 Instrumentation: Skipped - NEXT_RUNTIME is', process.env.NEXT_RUNTIME);
    }
}
