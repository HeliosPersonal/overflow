/**
 * Environment Configuration Loader
 *
 * APP_ENV controls which .env file and whether to use Infisical:
 * - development → .env.development only
 * - staging/production → .env file + Infisical secrets
 */

import { createLogger } from '@/lib/logger';

const logger = createLogger('infisical');

/** Configuration for Infisical secret management */
interface InfisicalCredentials {
    clientId: string;
    clientSecret: string;
    projectId: string;
    environment: 'staging' | 'production';
}

/** Map of environment variable key-value pairs */
interface EnvironmentVariables {
    [key: string]: string;
}

const INFISICAL_API_URL = 'https://eu.infisical.com';
const APP_ENV_DEVELOPMENT = 'development';
const APP_ENV_STAGING = 'staging';
const APP_ENV_PRODUCTION = 'production';

/**
 * Infisical folder paths for application secrets.
 * Secrets use SCREAMING_SNAKE_CASE naming with __ as section separator.
 * Infrastructure secrets live in /infra (consumed by CI/CD only, not loaded here).
 */
const APP_SECRET_PATHS = [
    '/app/connections',
    '/app/auth',
    '/app/services',
];

/**
 * Load all environment configuration
 *
 * @returns Merged environment variables from .env file and optionally Infisical
 */
export async function loadEnvironmentConfiguration(): Promise<EnvironmentVariables> {
    if (!isNodeJsRuntime()) {
        logger.warn('Cannot load configuration in Edge Runtime');
        return {};
    }

    const appEnvironment = determineAppEnvironment();
    const useInfisical = shouldFetchFromInfisical(appEnvironment);

    const baseConfig = await loadEnvFile(appEnvironment);
    const secrets = useInfisical ? await fetchInfisicalSecrets(appEnvironment) : {};
    const allVariables = { ...baseConfig, ...secrets };

    injectEnvironmentVariables(allVariables);

    logger.info(
        { appEnv: appEnvironment, total: Object.keys(allVariables).length, fromFile: Object.keys(baseConfig).length, fromInfisical: Object.keys(secrets).length },
        'Configuration loaded',
    );

    return allVariables;
}

/** Determine application environment from APP_ENV (defaults to 'development') */
function determineAppEnvironment(): string {
    const appEnv = process.env.APP_ENV?.toLowerCase();

    // Validate and return known environments
    if (appEnv === APP_ENV_STAGING || appEnv === APP_ENV_PRODUCTION) {
        return appEnv;
    }

    // Default to development
    return APP_ENV_DEVELOPMENT;
}

/** Check if Infisical should be used (only for staging/production) */
function shouldFetchFromInfisical(appEnvironment: string): boolean {
    return appEnvironment === APP_ENV_STAGING || appEnvironment === APP_ENV_PRODUCTION;
}

/** Check if running in Node.js runtime (not Edge runtime) */
function isNodeJsRuntime(): boolean {
    return typeof process !== 'undefined';
}

/** Inject environment variables into process.env */
function injectEnvironmentVariables(variables: EnvironmentVariables): void {
    Object.entries(variables).forEach(([key, value]) => {
        process.env[key] = value;
    });

    const criticalVars = ['AUTH_KEYCLOAK_ISSUER', 'API_URL'];
    const missing = criticalVars.filter(name => !process.env[name]);
    if (missing.length) {
        logger.warn({ missing }, 'Missing critical environment variables');
    }
}

/** Load environment variables from .env file */
async function loadEnvFile(environment: string): Promise<EnvironmentVariables> {
    if (!isNodeJsRuntime()) return {};

    try {
        const dotenv = await import('dotenv');
        const path = await import('path');
        const envFileName = `.env.${environment}`;
        const envFilePath = path.resolve(process.cwd(), envFileName);

        const result = dotenv.config({ path: envFilePath });

        if (result.error) {
            logger.warn({ envFile: envFileName, error: result.error.message }, 'Could not read env file');
            return {};
        }

        const parsed = result.parsed || {};
        logger.debug({ envFile: envFileName, count: Object.keys(parsed).length }, 'Loaded env file');
        return parsed;

    } catch (error) {
        logger.error({ err: error }, 'Failed to load env file');
        return {};
    }
}

/** Fetch secrets from Infisical secret management service */
async function fetchInfisicalSecrets(environment: string): Promise<EnvironmentVariables> {
    const credentials = getInfisicalCredentials(environment);

    if (!credentials) {
        logger.warn('Infisical credentials missing — skipping');
        return {};
    }

    try {
        const { InfisicalSDK } = await import('@infisical/sdk');
        const client = new InfisicalSDK({ siteUrl: INFISICAL_API_URL });

        await client.auth().universalAuth.login({
            clientId: credentials.clientId,
            clientSecret: credentials.clientSecret,
        });

        let allSecrets: Array<{ secretKey: string; secretValue: string }> = [];

        for (const secretPath of APP_SECRET_PATHS) {
            const response = await client.secrets().listSecrets({
                environment: credentials.environment,
                projectId: credentials.projectId,
                secretPath,
            });
            allSecrets = allSecrets.concat(response.secrets || []);
        }

        logger.debug({ environment: credentials.environment, count: allSecrets.length }, 'Fetched secrets from Infisical');
        return transformSecretsToEnvFormat(allSecrets);

    } catch (error) {
        logger.error({ err: error }, 'Failed to fetch Infisical secrets');

        if (process.env.NODE_ENV === 'production') {
            throw new Error('Critical: Failed to load required secrets from Infisical');
        }

        return {};
    }
}

/** Get and validate Infisical credentials from environment */
function getInfisicalCredentials(appEnvironment: string): InfisicalCredentials | null {
    const clientId = process.env.INFISICAL_CLIENT_ID || '';
    const clientSecret = process.env.INFISICAL_CLIENT_SECRET || '';
    const projectId = process.env.INFISICAL_PROJECT_ID || '';

    if (!clientId || !clientSecret || !projectId) {
        logger.warn({
            hasClientId: !!clientId,
            hasClientSecret: !!clientSecret,
            hasProjectId: !!projectId,
        }, 'Missing Infisical credentials');
        return null;
    }

    return {
        clientId,
        clientSecret,
        projectId,
        environment: appEnvironment === APP_ENV_PRODUCTION ? 'production' : 'staging',
    };
}

/**
 * Transform Infisical secrets to environment variable format.
 *
 * Secrets are stored as SCREAMING_SNAKE_CASE in Infisical (e.g. NEXTAUTH__KEYCLOAK_CLIENT_SECRET).
 * The transform replaces __ with _ and uppercases (both are no-ops for SCREAMING_SNAKE input).
 *
 * Examples:
 *   NEXTAUTH__KEYCLOAK_CLIENT_SECRET   → NEXTAUTH_KEYCLOAK_CLIENT_SECRET
 *   KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID  → KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID
 *   NOTIFICATION__INTERNAL_API_KEY     → NOTIFICATION_INTERNAL_API_KEY
 */
function transformSecretsToEnvFormat(secrets: Array<{ secretKey: string; secretValue: string }>): EnvironmentVariables {
    const transformedSecrets: EnvironmentVariables = {};

    secrets.forEach((secret) => {
        const originalKey = secret.secretKey;
        const normalizedKey = originalKey.replace(/__/g, '_');
        const envVarKey = normalizedKey
            .replace(/([a-z])([A-Z])/g, '$1_$2')
            .toUpperCase();

        logger.debug({ from: originalKey, to: envVarKey }, 'Secret key transform');
        transformedSecrets[envVarKey] = secret.secretValue;
    });

    return transformedSecrets;
}

