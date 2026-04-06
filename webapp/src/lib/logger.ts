import pino from 'pino';

const isDev = process.env.NODE_ENV !== 'production';

/**
 * Structured JSON logger (pino).
 *
 * - **Production / Staging**: emits newline-delimited JSON to stdout (Loki-friendly).
 *   Errors are serialized with stack traces in the `err` field.
 * - **Development**: uses `pino-pretty` for coloured, human-readable output.
 *
 * Usage:
 *   import logger, { createLogger } from '@/lib/logger';
 *   const log = createLogger('my-module');  // adds { module: 'my-module' } to every entry
 *   log.info('Hello');
 *   log.error({ err }, 'Something went wrong');
 */
const logger = pino({
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

/** Returns a child logger with a `module` field on every log entry. */
export function createLogger(module: string) {
    return logger.child({ module });
}

export default logger;

