/**
 * Pino → OpenTelemetry Logs bridge
 *
 * Creates a Node.js Writable stream that pino can write JSON lines into.
 * Each line is parsed and forwarded to an OTEL LoggerProvider so logs are
 * exported via OTLP to Grafana Alloy (same pipeline as .NET services).
 *
 * Usage:
 *   const stream = createOtelLogStream(provider.getLogger('pino-bridge'));
 *   // then add `stream` to pino's multistream
 */

import { Writable } from 'stream';
import type { AnyValueMap } from '@opentelemetry/api-logs';

/** Minimal duck-type covering the Logger.emit() method we actually need */
interface OtelEmitter {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    emit(record: any): void;
}

/**
 * Map pino numeric level → OTEL SeverityNumber.
 * OTEL spec: TRACE=1, DEBUG=5, INFO=9, WARN=13, ERROR=17, FATAL=21
 */
function pinoLevelToOtelSeverity(level: number): number {
    if (level >= 60) return 21; // FATAL
    if (level >= 50) return 17; // ERROR
    if (level >= 40) return 13; // WARN
    if (level >= 30) return 9;  // INFO
    if (level >= 20) return 5;  // DEBUG
    return 1;                   // TRACE
}

const PINO_LEVEL_NAMES: Record<number, string> = {
    10: 'trace',
    20: 'debug',
    30: 'info',
    40: 'warn',
    50: 'error',
    60: 'fatal',
};

/**
 * Returns a Writable stream that accepts newline-delimited pino JSON and emits
 * each record to the provided OTEL Logger.
 */
export function createOtelLogStream(otelLogger: OtelEmitter): Writable {
    return new Writable({
        objectMode: false,
        decodeStrings: false,
        write(chunk: Buffer | string, _enc, callback) {
            try {
                const line = (typeof chunk === 'string' ? chunk : chunk.toString()).trim();
                if (line) {
                    const { level, time, msg, pid: _pid, hostname: _hostname, ...attrs } = JSON.parse(line);
                    const levelNum: number = typeof level === 'number' ? level : 30;

                    otelLogger.emit({
                        severityNumber: pinoLevelToOtelSeverity(levelNum),
                        severityText: PINO_LEVEL_NAMES[levelNum] ?? String(level),
                        body: typeof msg === 'string' ? msg : String(msg ?? ''),
                        attributes: attrs as AnyValueMap,
                        timestamp: time ? new Date(time as string) : new Date(),
                    });
                }
            } catch {
                // Ignore malformed JSON lines — never crash the log stream
            }
            callback();
        },
    });
}
