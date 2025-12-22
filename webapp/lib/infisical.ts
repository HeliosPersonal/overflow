/**
 * Infisical configuration loader for Next.js
 * Loads configuration from .env files and secrets from Infisical at build time
 *
 * Configuration (non-sensitive) → .env.staging / .env.production
 * Secrets (sensitive) → Infisical
 *
 * IMPORTANT: This module uses Node.js APIs and should only be imported in Node.js runtime.
 * It's imported dynamically in instrumentation.ts to avoid Edge Runtime issues.
 */

// Type-only imports are safe in Edge Runtime
import type { InfisicalSDK } from '@infisical/sdk';

interface InfisicalConfig {
  clientId: string;
  clientSecret: string;
  projectId: string;
  environment: string;
}

interface SecretsMap {
  [key: string]: string;
}

/**
 * Load configuration and secrets
 * 1. Load config from .env.{environment} file
 * 2. Load secrets from Infisical
 * 3. Merge (secrets override config if same key exists)
 */
export async function loadConfiguration(): Promise<SecretsMap> {
  // Guard against Edge Runtime - check only for process existence
  if (typeof process === 'undefined') {
    console.warn('⚠️ loadConfiguration called in Edge Runtime, skipping');
    return {};
  }

  // Use APP_ENV to determine environment (staging vs production)
  // APP_ENV is set by Kubernetes deployment, defaults to production
  const environment = process.env.APP_ENV || 'production';
  
  console.log(`🔧 Loading configuration for environment: ${environment}...`);

  // 1. Load non-sensitive config from .env file
  const configMap = await loadEnvFile(environment);

  // 2. Load sensitive secrets from Infisical
  const secretsMap = await loadInfisicalSecrets(environment);
  
  // 3. Merge: secrets take precedence over config
  const merged = { ...configMap, ...secretsMap };
  
  // 4. Set all as environment variables
  Object.entries(merged).forEach(([key, value]) => {
    process.env[key] = value;
  });
  
  console.log(`✅ Configuration loaded: ${Object.keys(configMap).length} config vars + ${Object.keys(secretsMap).length} secrets`);
  
  return merged;
}

/**
 * Load non-sensitive configuration from .env file
 */
async function loadEnvFile(environment: string): Promise<SecretsMap> {
  // Guard against Edge Runtime
  if (typeof process === 'undefined') {
    return {};
  }

  try {
    // Dynamic imports to avoid Edge Runtime
    const dotenv = await import('dotenv');
    const pathModule = await import('path');

    const envFile = `.env.${environment}`;
    const envPath = pathModule.resolve(process.cwd(), envFile);

    const result = dotenv.config({ path: envPath });

    if (result.error) {
      console.warn(`⚠️  Could not load ${envFile}:`, result.error.message);
      return {};
    }
    
    console.log(`✅ Loaded configuration from ${envFile}`);
    return result.parsed || {};
  } catch (error) {
    console.warn(`⚠️  Failed to load env file:`, error);
    return {};
  }
}

/**
 * Load sensitive secrets from Infisical
 */
async function loadInfisicalSecrets(environment: string): Promise<SecretsMap> {
  const infisicalConfig: InfisicalConfig = {
    clientId: process.env.INFISICAL_CLIENT_ID || '',
    clientSecret: process.env.INFISICAL_CLIENT_SECRET || '',
    projectId: process.env.INFISICAL_PROJECT_ID || '',
    environment: environment === 'production' ? 'production' : 'staging',
  };

  // Validate Infisical config
  if (!infisicalConfig.clientId || !infisicalConfig.clientSecret || !infisicalConfig.projectId) {
    console.warn(
      '⚠️  Infisical credentials not found. Skipping secret loading.',
    );
    return {};
  }

  console.log(
    `🔐 Loading secrets from Infisical (Environment: ${infisicalConfig.environment})...`,
  );

  try {
    // Dynamic import to avoid Edge Runtime issues
    const { InfisicalSDK } = await import('@infisical/sdk');

    const client = new InfisicalSDK({
      siteUrl: 'https://eu.infisical.com',
    });

    // Authenticate
    await client.auth().universalAuth.login({
      clientId: infisicalConfig.clientId,
      clientSecret: infisicalConfig.clientSecret,
    });

    // Fetch all secrets
    const response = await client.secrets().listSecrets({
      environment: infisicalConfig.environment,
      projectId: infisicalConfig.projectId,
      secretPath: '/',
    });

    // response.secrets is the array of secrets
    const allSecrets = response.secrets || [];

    // Convert to key-value map and transform keys
    const secretsMap: SecretsMap = {};
    allSecrets.forEach((secret) => {
      // Convert double underscore to single underscore for env var format
      // Example: Auth__Secret → Auth_Secret (keeping original case)
      // Then uppercase the whole thing: Auth_Secret → AUTH_SECRET
      let key = secret.secretKey.replace(/__/g, '_');
      
      // For Next.js NEXT_PUBLIC_ vars, keep them uppercase with underscores
      // For other vars, convert to SCREAMING_SNAKE_CASE
      if (key.startsWith('NextPublic_')) {
        key = key.replace('NextPublic_', 'NEXT_PUBLIC_');
      } else if (key.startsWith('ApiUrl_')) {
        key = key.replace('ApiUrl_', 'API_URL_');
      } else {
        // Convert PascalCase_PascalCase to SCREAMING_SNAKE_CASE
        // Auth_Url → AUTH_URL
        key = key
          .replace(/([a-z])([A-Z])/g, '$1_$2') // Add underscore between lowercase and uppercase
          .toUpperCase();
      }
      
      secretsMap[key] = secret.secretValue;
    });

    console.log(`✅ Loaded ${allSecrets.length} secrets from Infisical`);

    return secretsMap;
  } catch (error) {
    console.error('❌ Failed to load secrets from Infisical:', error);
    // In production, we might want to throw
    if (process.env.NODE_ENV === 'production') {
      throw new Error('Failed to load required secrets from Infisical');
    }
    return {};
  }
}