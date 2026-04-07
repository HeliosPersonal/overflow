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
 *   import { createLogger } from '@/lib/logger';
 *   const log = createLogger('my-module');  // adds { module: 'my-module' } to every entry
 *   log.info('Hello');
 *   log.error({ err }, 'Something went wrong');
 */

// ─── Global singleton key ─────────────────────────────────────────────────────
// Using globalThis (not module-level variable) ensures the same Logger instance
// is shared across all Next.js webpack module instances (server bundle, edge
// bundle, RSC, etc.).  Without this, _setLogger() in the Node.js context would
// not propagate to loggers created in other chunks.

const LOGGER_KEY = Symbol.for('overflow.pino.logger');

function getGlobalLogger(): Logger {
    if (!(globalThis as any)[LOGGER_KEY]) {
        (globalThis as any)[LOGGER_KEY] = pino({
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
    }
    return (globalThis as any)[LOGGER_KEY] as Logger;
}

// ─── Internal setter (used by logger.node.ts only) ────────────────────────────

/**
 * Replace the global logger with a new instance (e.g. multistream with OTEL).
 * Called exclusively from `logger.node.ts` — never call this from other modules.
 * All existing `createLogger()` proxies pick up the new instance on their next call.
 */
export function _setLogger(newLogger: Logger): void {
    (globalThis as any)[LOGGER_KEY] = newLogger;
}

// ─── Public factory ───────────────────────────────────────────────────────────

/**
 * Returns a child logger with a `module` field on every log entry.
 *
 * The returned object is a Proxy that reads from the globalThis singleton on
 * every access so it transparently switches to the OTEL-multistream logger
 * once `_setLogger()` is called from `logger.node.ts`.
 */
export function createLogger(module: string): Logger {
    let _child: Logger | null = null;
    let _parent: Logger | null = null;

    return new Proxy({} as Logger, {
        get(_, prop: string | symbol) {
            const current = getGlobalLogger();
            // Recreate child when the global logger was swapped (_setLogger upgrade)
            if (_parent !== current) {
                _child = current.child({ module });
                _parent = current;
            }
            const val = (_child as any)[prop];
            return typeof val === 'function' ? val.bind(_child) : val;
        },
    });
}

// Proxy-based default export so `import logger from '@/lib/logger'` also
// picks up the upgraded multistream instance after _setLogger().
const _defaultProxy = new Proxy({} as Logger, {
    get(_, prop: string | symbol) {
        const val = (getGlobalLogger() as any)[prop];
        return typeof val === 'function' ? val.bind(getGlobalLogger()) : val;
    },
});
export default _defaultProxy as Logger;

