/**
 * Node.js-only Instrumentation
 *
 * This file is dynamically imported by instrumentation.ts ONLY when
 * NEXT_RUNTIME === 'nodejs'. The Edge bundler never touches this file,
 * so all Node.js-only APIs (process.cwd, path, process.on, etc.) are safe to use.
 *
 * Initializes:
 * 1. Environment configuration (Infisical secrets / .env files)
 * 2. OpenTelemetry (traces + metrics → Grafana Alloy via OTLP)
 *
 * @see https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */

import { createLogger } from '@/lib/logger';

const logger = createLogger('instrumentation');

const PHASE_PRODUCTION_BUILD = 'phase-production-build';

export async function register(): Promise<void> {
    const currentPhase = process.env.NEXT_PHASE;

    logger.info({ phase: currentPhase, nodeEnv: process.env.NODE_ENV, appEnv: process.env.APP_ENV },
        'Starting application bootstrap');

    if (currentPhase === PHASE_PRODUCTION_BUILD) {
        logger.info('Skipped — build phase');
        return;
    }

    // Load secrets first so OTEL env vars are available
    await initializeEnvironmentConfiguration();

    // Start OTEL traces + metrics (reads OTEL_EXPORTER_OTLP_* from process.env)
    await initializeOpenTelemetry();

    // Attach OTLP log exporter to pino (same endpoint, same Alloy pipeline)
    await initializeOtelLogging();
}

/**
 * Load environment configuration — .env files + Infisical secrets (non-dev only).
 */
async function initializeEnvironmentConfiguration(): Promise<void> {
    try {
        logger.info('Loading environment configuration...');
        const { loadEnvironmentConfiguration } = await import('./infisical');
        const loadedVariables = await loadEnvironmentConfiguration();
        const variableCount = Object.keys(loadedVariables).length;
        logger.info({ variableCount }, 'Configuration loaded');
    } catch (error) {
        logger.error({ err: error }, 'Failed to load configuration');
        logger.warn('App will start with existing environment variables');
    }
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
        logger.info('OTEL skipped — OTEL_EXPORTER_OTLP_ENDPOINT not set');
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
                    // Next.js 16 + Node.js 20: instrumentation packages that patch the global
                    // fetch / http / undici cause TransformStream corruption at runtime
                    // ("controller[kState].transformAlgorithm is not a function").
                    // Disable ALL network-layer instrumentations; Next.js has its own built-in
                    // OTEL tracing for incoming requests via the register() hook.
                    '@opentelemetry/instrumentation-http': { enabled: false },
                    '@opentelemetry/instrumentation-undici': { enabled: false },
                    '@opentelemetry/instrumentation-fs': { enabled: false },
                    '@opentelemetry/instrumentation-net': { enabled: false },
                    '@opentelemetry/instrumentation-dns': { enabled: false },
                }),
            ],
        });

        sdk.start();
        logger.info({ endpoint }, 'OTEL SDK started');

        process.on('SIGTERM', () => {
            sdk.shutdown()
                .then(() => logger.info('OTEL SDK shut down gracefully'))
                .catch((err) => logger.error({ err }, 'OTEL SDK shutdown error'));
        });
    } catch (error) {
        logger.error({ err: error }, 'Failed to initialize OpenTelemetry');
        // Non-fatal
    }
}

/**
 * Attach an OTLP log exporter to pino via a custom Writable stream.
 *
 * This runs AFTER initializeEnvironmentConfiguration() so OTEL_EXPORTER_OTLP_ENDPOINT
 * is already in process.env (loaded from .env.staging / Infisical).
 * All existing createLogger() proxies automatically pick up the new multistream
 * instance on their next log call — no restart needed.
 */
async function initializeOtelLogging(): Promise<void> {
    const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

    if (!endpoint) {
        logger.info('OTEL log exporter skipped — OTEL_EXPORTER_OTLP_ENDPOINT not set');
        return;
    }

    try {
        const { OTLPLogExporter } = await import('@opentelemetry/exporter-logs-otlp-http');
        const { LoggerProvider, BatchLogRecordProcessor } = await import('@opentelemetry/sdk-logs');
        const { resourceFromAttributes } = await import('@opentelemetry/resources');
        const { ATTR_SERVICE_NAME } = await import('@opentelemetry/semantic-conventions');
        const { createOtelLogStream } = await import('./lib/otel-log-stream');
        const { addOtelStream } = await import('./lib/logger.node');

        // Parse shared OTLP headers (e.g. for cloud Grafana auth)
        const headersRaw = process.env.OTEL_EXPORTER_OTLP_HEADERS ?? '';
        const headers: Record<string, string> = {};
        if (headersRaw) {
            for (const part of headersRaw.split(',')) {
                const [k, ...v] = part.split('=');
                if (k && v.length) headers[k.trim()] = v.join('=').trim();
            }
        }

        const appEnv = process.env.APP_ENV ?? 'production';

        const provider = new LoggerProvider({
            resource: resourceFromAttributes({
                [ATTR_SERVICE_NAME]: 'overflow-webapp',
                'deployment.environment': appEnv,
            }),
            processors: [
                new BatchLogRecordProcessor(
                    new OTLPLogExporter({ url: `${endpoint}/v1/logs`, headers }),
                    // Flush every 2 s (default is 5 s) so logs appear in Grafana quickly
                    { scheduledDelayMillis: 2_000 },
                ),
            ],
        });

        addOtelStream(createOtelLogStream(provider.getLogger('pino-bridge')));
        logger.info({ endpoint }, 'OTEL log stream attached to pino');

        // Periodic heartbeat — disabled by default; enable via LOG_HEARTBEAT_MS env var (ms).
        const heartbeatMs = parseInt(process.env.LOG_HEARTBEAT_MS ?? '0', 10);
        if (heartbeatMs > 0) {
            const ticker = setInterval(() => {
                logger.info({ uptimeSec: Math.round(process.uptime()) }, 'App heartbeat');
            }, heartbeatMs);
            ticker.unref(); // don't prevent graceful shutdown
        }

        process.on('SIGTERM', () => {
            provider.shutdown()
                .then(() => logger.info('OTEL log provider shut down gracefully'))
                .catch((err) => logger.error({ err }, 'OTEL log provider shutdown error'));
        });
    } catch (error) {
        logger.error({ err: error }, 'Failed to initialize OTEL log exporter');
        // Non-fatal — app continues with stdout-only logging
    }
}

