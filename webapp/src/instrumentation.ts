/**
 * Next.js Instrumentation Hook
 *
 * This file runs ONCE when the Next.js server starts, before any pages load.
 * It initializes the application's environment configuration.
 *
 * Configuration Strategy:
 * - Local Development: Load from .env.development file only
 * - Staging/Production: Load from .env files + fetch secrets from Infisical
 *
 * @see https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */

const RUNTIME_NODEJS = 'nodejs';
const PHASE_PRODUCTION_BUILD = 'phase-production-build';

/**
 * Next.js instrumentation register hook
 * Automatically called by Next.js during server initialization
 */
export async function register() {
    const currentRuntime = process.env.NEXT_RUNTIME;
    const currentPhase = process.env.NEXT_PHASE;
    const nodeEnvironment = process.env.NODE_ENV;
    const appEnvironment = process.env.APP_ENV;

    console.log('🚀 [Instrumentation] Starting application bootstrap', {
        runtime: currentRuntime,
        phase: currentPhase,
        nodeEnv: nodeEnvironment,
        appEnv: appEnvironment,
    });

    const isNodeRuntime = currentRuntime === RUNTIME_NODEJS;
    const isBuildPhase = currentPhase === PHASE_PRODUCTION_BUILD;

    // Only initialize in Node.js runtime (skip Edge runtime)
    if (!isNodeRuntime) {
        console.log(`⏭️  [Instrumentation] Skipped - Edge runtime detected (${currentRuntime})`);
        return;
    }

    // Skip during build phase - configuration is only needed at runtime
    if (isBuildPhase) {
        console.log('⏭️  [Instrumentation] Skipped - Build phase, configuration not needed');
        return;
    }

    // Load environment configuration and secrets
    await initializeEnvironmentConfiguration();
}

/**
 * Initialize environment configuration
 * Loads .env files and fetches secrets from Infisical (non-dev environments only)
 */
async function initializeEnvironmentConfiguration(): Promise<void> {
    try {
        console.log('🔧 [Instrumentation] Loading environment configuration...');

        const { loadEnvironmentConfiguration } = await import('./infisical');
        const loadedVariables = await loadEnvironmentConfiguration();

        const variableCount = Object.keys(loadedVariables).length;
        console.log(`✅ [Instrumentation] Configuration loaded successfully (${variableCount} variables)`);

    } catch (error) {
        console.error('❌ [Instrumentation] Failed to load configuration:', error);
        console.warn('⚠️  [Instrumentation] Application will start with existing environment variables');
        // Non-fatal: Allow app to start even if configuration loading fails
    }
}

