/**
 * Environment Configuration Loader
 *
 * APP_ENV controls which .env file and whether to use Infisical:
 * - development → .env.development only
 * - staging/production → .env file + Infisical secrets
 */

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
const SECRET_PATH_ROOT = '/';
const APP_ENV_DEVELOPMENT = 'development';
const APP_ENV_STAGING = 'staging';
const APP_ENV_PRODUCTION = 'production';

/**
 * Load all environment configuration
 *
 * @returns Merged environment variables from .env file and optionally Infisical
 */
export async function loadEnvironmentConfiguration(): Promise<EnvironmentVariables> {
    if (!isNodeJsRuntime()) {
        console.warn('⚠️  [Config] Cannot load configuration in Edge Runtime');
        return {};
    }

    const appEnvironment = determineAppEnvironment();
    const useInfisical = shouldFetchFromInfisical(appEnvironment);

    console.log('🔧 [Config] Environment:', {
        APP_ENV: appEnvironment,
        useInfisical
    });

    const baseConfig = await loadEnvFile(appEnvironment);
    const secrets = useInfisical ? await fetchInfisicalSecrets(appEnvironment) : {};
    const allVariables = { ...baseConfig, ...secrets };

    injectEnvironmentVariables(allVariables);

    console.log(`✅ [Config] Loaded ${allVariables.length} variables (${baseConfig.length} base + ${secrets.length} secrets)`);

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

    // TODO: Removed Cloudinary from critical vars check
    const criticalVars = ['AUTH_KEYCLOAK_ISSUER', 'API_URL'];
    const status = criticalVars.map(name => {
        const value = process.env[name];
        return `${name}: ${value ? value.substring(0, 8) + '...' : '❌'}`;
    });

    console.log(`💉 [Config] Injected ${Object.keys(variables).length} variables`);
    console.log(`🔍 [Config] ${status.join(' | ')}`);
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
            console.warn(`⚠️  [Config] Could not read ${envFileName}: ${result.error.message}`);
            return {};
        }

        const parsed = result.parsed || {};
        console.log(`📄 [Config] Loaded ${Object.keys(parsed).length} variables from ${envFileName}`);
        return parsed;

    } catch (error) {
        console.error(`❌ [Config] Failed to load env file:`, error);
        return {};
    }
}

/** Fetch secrets from Infisical secret management service */
async function fetchInfisicalSecrets(environment: string): Promise<EnvironmentVariables> {
    const credentials = getInfisicalCredentials(environment);

    if (!credentials) {
        console.warn('⚠️  [Config] Infisical credentials missing - skipping');
        return {};
    }

    console.log(`🔐 [Config] Fetching secrets from Infisical (${credentials.environment})...`);

    try {
        const { InfisicalSDK } = await import('@infisical/sdk');
        const client = new InfisicalSDK({ siteUrl: INFISICAL_API_URL });

        await client.auth().universalAuth.login({
            clientId: credentials.clientId,
            clientSecret: credentials.clientSecret,
        });

        const response = await client.secrets().listSecrets({
            environment: credentials.environment,
            projectId: credentials.projectId,
            secretPath: SECRET_PATH_ROOT,
        });

        const secrets = response.secrets || [];
        const transformed = transformSecretsToEnvFormat(secrets);

        console.log(`✅ [Config] Loaded ${secrets.length} secrets from Infisical`);
        return transformed;

    } catch (error) {
        const errorMessage = error instanceof Error ? error.message : String(error);
        console.error(`❌ [Config] Failed to fetch Infisical secrets: ${errorMessage}`);

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
        console.warn('⚠️  [Config] Missing Infisical credentials:', {
            hasClientId: !!clientId,
            hasClientSecret: !!clientSecret,
            hasProjectId: !!projectId,
        });
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
 * Transform Infisical secrets to environment variable format
 *
 * Converts: Auth__Secret → AUTH_SECRET
 */
function transformSecretsToEnvFormat(secrets: Array<{ secretKey: string; secretValue: string }>): EnvironmentVariables {
    const transformedSecrets: EnvironmentVariables = {};

    secrets.forEach((secret) => {
        const originalKey = secret.secretKey;
        const normalizedKey = originalKey.replace(/__/g, '_');
        const envVarKey = normalizedKey
            .replace(/([a-z])([A-Z])/g, '$1_$2')
            .toUpperCase();

        console.log(`🔄 [Config] ${originalKey} → ${envVarKey}`);
        transformedSecrets[envVarKey] = secret.secretValue;
    });

    return transformedSecrets;
}

