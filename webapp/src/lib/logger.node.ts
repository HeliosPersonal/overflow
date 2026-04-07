/**
 * Node.js-only logger extensions.
 *
 * This file MUST NOT be imported from Edge Runtime contexts (middleware, edge routes).
 * It is only imported from `instrumentation.node.ts` which is already guarded by
 * the `NEXT_RUNTIME === 'nodejs'` check.
 *
 * Provides `addOtelStream()` — upgrades the shared pino logger to a multistream
 * that writes to both stdout and an OTLP log stream.
 */

import pino from 'pino';
import type { Writable } from 'stream';
import { trace } from '@opentelemetry/api';
import { _setLogger } from './logger';

const isDev = process.env.NODE_ENV !== 'production';


/**
 * Attach an OTLP destination stream to pino via multistream.
 * Called from `instrumentation.node.ts` after the OTLP endpoint is known.
 * No-op in development (stdout-only logging is fine there).
 *
 * After this call all existing `createLogger()` proxies automatically start
 * writing to both stdout and the OTEL stream on their next log call.
 */
export function addOtelStream(otelStream: Writable): void {
    if (isDev) return;

    // Use pino.destination(1) (fd 1 = stdout) instead of process.stdout directly.
    // This avoids Next.js static analysis flagging "process.stdout" as a
    // Node.js-only API when it scans files for Edge Runtime compatibility.
    const stdoutDest = pino.destination({ fd: 1, sync: false });

    const upgraded = pino(
        {
            level: process.env.LOG_LEVEL ?? 'info',
            serializers: pino.stdSerializers,
            formatters: { level: (label: string) => ({ level: label }) },
            timestamp: pino.stdTimeFunctions.isoTime,
            // Inject active OTEL span context into every log record so logs are
            // correlated with traces in Grafana (trace_id / span_id fields).
            mixin() {
                const span = trace.getActiveSpan();
                if (!span?.isRecording()) return {};
                const ctx = span.spanContext();
                return { trace_id: ctx.traceId, span_id: ctx.spanId, trace_flags: ctx.traceFlags };
            },
        },
        pino.multistream([
            { stream: stdoutDest, level: 'info' as pino.Level },
            { stream: otelStream, level: 'info' as pino.Level },
        ]),
    );

    _setLogger(upgraded);
}

