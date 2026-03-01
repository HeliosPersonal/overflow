/**
 * Next.js Instrumentation Hook
 *
 * This file runs ONCE when the Next.js server starts, before any pages load.
 * It initializes:
 * 1. OpenTelemetry (traces + metrics → Grafana Alloy via OTLP)
 * 2. Environment configuration (Infisical secrets)
 *
 * @see https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */

const RUNTIME_NODEJS = 'nodejs';
const PHASE_PRODUCTION_BUILD = 'phase-production-build';

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

    if (!isNodeRuntime) {
        console.log(`⏭️  [Instrumentation] Skipped - Edge runtime detected (${currentRuntime})`);
        return;
    }

    if (isBuildPhase) {
        console.log('⏭️  [Instrumentation] Skipped - Build phase');
        return;
    }

    // Load secrets first so OTEL env vars are available
    await initializeEnvironmentConfiguration();

    // Then start OTEL (reads OTEL_EXPORTER_OTLP_* from process.env)
    await initializeOpenTelemetry();
}

/**
 * Initialize OpenTelemetry SDK — traces + metrics exported via OTLP to Grafana Alloy.
 * Reads endpoint/headers from env vars set by Infisical:
 *   OTEL_EXPORTER_OTLP_ENDPOINT  — e.g. http://grafana-alloy.monitoring.svc.cluster.local:4318
 *   OTEL_EXPORTER_OTLP_HEADERS   — e.g. Authorization=Bearer xxx  (optional, for cloud Grafana)
 */
async function initializeOpenTelemetry(): Promise<void> {
    const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

    if (!endpoint) {
        console.log('⏭️  [OTEL] Skipped — OTEL_EXPORTER_OTLP_ENDPOINT not set');
        return;
    }

    try {
        const { NodeSDK } = await import('@opentelemetry/sdk-node');
        const { getNodeAutoInstrumentations } = await import('@opentelemetry/auto-instrumentations-node');
        const { OTLPTraceExporter } = await import('@opentelemetry/exporter-trace-otlp-http');
        const { OTLPMetricExporter } = await import('@opentelemetry/exporter-metrics-otlp-http');
        const { PeriodicExportingMetricReader } = await import('@opentelemetry/sdk-metrics');
        const { resourceFromAttributes } = await import('@opentelemetry/resources');
        const { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } = await import('@opentelemetry/semantic-conventions');

        const appEnv = process.env.APP_ENV ?? 'development';
        const headersRaw = process.env.OTEL_EXPORTER_OTLP_HEADERS ?? '';

        // Parse "Key=Value,Key2=Value2" header string
        const headers: Record<string, string> = {};
        if (headersRaw) {
            for (const part of headersRaw.split(',')) {
                const [k, ...v] = part.split('=');
                if (k && v.length) headers[k.trim()] = v.join('=').trim();
            }
        }

        const sdk = new NodeSDK({
            resource: resourceFromAttributes({
                [ATTR_SERVICE_NAME]: 'overflow-webapp',
                [ATTR_SERVICE_VERSION]: process.env.npm_package_version ?? '1.0.0',
                'deployment.environment': appEnv,
            }),
            traceExporter: new OTLPTraceExporter({
                url: `${endpoint}/v1/traces`,
                headers,
            }),
            metricReader: new PeriodicExportingMetricReader({
                exporter: new OTLPMetricExporter({
                    url: `${endpoint}/v1/metrics`,
                    headers,
                }),
                exportIntervalMillis: 60_000,
            }),
            instrumentations: [
                getNodeAutoInstrumentations({
                    '@opentelemetry/instrumentation-fs': { enabled: false }, // too noisy
                    '@opentelemetry/instrumentation-http': { enabled: true },
                    '@opentelemetry/instrumentation-net': { enabled: false },
                }),
            ],
        });

        sdk.start();
        console.log(`✅ [OTEL] SDK started — exporting to ${endpoint}`);

        process.on('SIGTERM', () => {
            sdk.shutdown()
                .then(() => console.log('✅ [OTEL] SDK shut down gracefully'))
                .catch((err) => console.error('❌ [OTEL] SDK shutdown error:', err));
        });
    } catch (error) {
        console.error('❌ [OTEL] Failed to initialize OpenTelemetry:', error);
        // Non-fatal
    }
}

/**
 * Load environment configuration — .env files + Infisical secrets (non-dev only).
 */
async function initializeEnvironmentConfiguration(): Promise<void> {
    try {
        console.log('🔧 [Instrumentation] Loading environment configuration...');
        const { loadEnvironmentConfiguration } = await import('./infisical');
        const loadedVariables = await loadEnvironmentConfiguration();
        const variableCount = Object.keys(loadedVariables).length;
        console.log(`✅ [Instrumentation] Configuration loaded (${variableCount} variables)`);
    } catch (error) {
        console.error('❌ [Instrumentation] Failed to load configuration:', error);
        console.warn('⚠️  [Instrumentation] App will start with existing environment variables');
    }
}

