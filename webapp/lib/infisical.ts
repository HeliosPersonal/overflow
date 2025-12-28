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
 * - Development: Only loads from .env files (no Infisical)
 * - Staging/Production: Loads from .env files + Infisical secrets
 *
 * 1. Load config from .env.{environment} file
 * 2. Load secrets from Infisical ONLY if not development
 * 3. Merge (secrets override config if same key exists)
 */
export async function loadConfiguration(): Promise<SecretsMap> {
  // Guard against Edge Runtime - check only for process existence
  if (typeof process === 'undefined') {
    console.warn('⚠️ loadConfiguration called in Edge Runtime, skipping');
    return {};
  }

  // Determine environment
  // NODE_ENV: 'development' | 'production' (Next.js build mode)
  // APP_ENV: 'staging' | 'production' (deployment environment)
  const nodeEnv = process.env.NODE_ENV || 'production';
  const isDevelopment = nodeEnv === 'development';

  // In development, use 'development' env file, otherwise use APP_ENV
  const appEnv = isDevelopment ? 'development' : (process.env.APP_ENV || 'production');

  console.log(`🔧 Loading configuration...`, {
    NODE_ENV: nodeEnv,
    APP_ENV: process.env.APP_ENV,
    effectiveEnv: appEnv,
    isDevelopment,
  });

  // 1. Load non-sensitive config from .env file
  const configMap = await loadEnvFile(appEnv);

  // 2. Load sensitive secrets from Infisical ONLY in non-development environments
  let secretsMap: SecretsMap = {};
  if (!isDevelopment) {
    console.log('🔐 Non-development environment detected, loading secrets from Infisical...');
    secretsMap = await loadInfisicalSecrets(appEnv);
  } else {
    console.log('🔧 Development environment detected, skipping Infisical (using .env files only)');
  }

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
      // Example: Auth__Secret → Auth_Secret
      // Then convert to SCREAMING_SNAKE_CASE: Auth_Secret → AUTH_SECRET
      let key = secret.secretKey.replace(/__/g, '_');
      
      console.log(`🔑 Infisical secret: ${secret.secretKey} → ${key}`);
      
      // Convert PascalCase_PascalCase to SCREAMING_SNAKE_CASE
      // Auth_Url → AUTH_URL
      // Cloudinary_ApiKey → CLOUDINARY_API_KEY
      key = key
        .replace(/([a-z])([A-Z])/g, '$1_$2') // Add underscore between lowercase and uppercase
        .toUpperCase();
      
      console.log(`   → Transformed to: ${key}`);
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