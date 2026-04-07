import pino, { type Logger } from 'pino';

const isDev = process.env.NODE_ENV !== 'production';

/**
 * Structured JSON logger (pino).
 *
 * - **Production / Staging**: emits newline-delimited JSON to stdout (Loki-friendly)
 *   AND — after `addOtelStream()` (logger.node.ts) is called — also pushes records
 *   via OTLP to Grafana Alloy (same pipeline as .NET services).
 * - **Development**: uses `pino-pretty` for coloured, human-readable output.
 *
 * ⚠ This file must stay Edge Runtime–safe (no process.stdout, no `stream` module).
 *   Node.js-only helpers live in `logger.node.ts`.
 *
 * Usage:
 *   import logger, { createLogger } from '@/lib/logger';
 *   const log = createLogger('my-module');  // adds { module: 'my-module' } to every entry
 *   log.info('Hello');
 *   log.error({ err }, 'Something went wrong');
 */

// ─── Internal mutable logger instance ────────────────────────────────────────
// Starts as stdout-only; replaced by logger.node.ts once the OTLP endpoint is
// known (after Infisical secrets are loaded in instrumentation.node.ts).

let _logger: Logger = pino({
    level: process.env.LOG_LEVEL ?? (isDev ? 'debug' : 'info'),
    serializers: pino.stdSerializers,
    ...(isDev
        ? {
            transport: {
                target: 'pino-pretty',
                options: { colorize: true, translateTime: 'HH:MM:ss.l', ignore: 'pid,hostname' },
            },
        }
        : {
            formatters: { level: (label: string) => ({ level: label }) },
            timestamp: pino.stdTimeFunctions.isoTime,
        }),
});

// ─── Internal setter (used by logger.node.ts only) ────────────────────────────

/**
 * Replace the module-level logger with a new instance (e.g. multistream with OTEL).
 * Called exclusively from `logger.node.ts` — never call this from other modules.
 * All existing `createLogger()` proxies pick up the new instance on their next call.
 */
export function _setLogger(newLogger: Logger): void {
    _logger = newLogger;
}

// ─── Public factory ───────────────────────────────────────────────────────────

/**
 * Returns a child logger with a `module` field on every log entry.
 *
 * The returned object is a Proxy that always delegates to the **current**
 * `_logger` — even after `_setLogger()` replaces it.  Child loggers created
 * before the OTLP upgrade therefore transparently start sending to Alloy
 * without needing to be recreated.
 */
export function createLogger(module: string): Logger {
    let _child: Logger | null = null;
    let _parent: Logger | null = null;

    return new Proxy({} as Logger, {
        get(_, prop: string | symbol) {
            // Recreate child when _logger was swapped (_setLogger upgrade)
            if (_parent !== _logger) {
                _child = _logger.child({ module });
                _parent = _logger;
            }
            const val = (_child as any)[prop];
            return typeof val === 'function' ? val.bind(_child) : val;
        },
    });
}

export default _logger;

