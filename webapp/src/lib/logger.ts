import pino from 'pino';

const isProduction = process.env.NODE_ENV === 'production';

/**
 * Structured JSON logger (pino).
 *
 * - **Production / Staging**: emits newline-delimited JSON to stdout so that
 *   Grafana Alloy (or any log collector) ingests each entry as a single log
 *   line in Loki — multi-line stack traces are embedded in a `stack` or `err`
 *   field instead of being split across separate lines.
 * - **Development**: uses `pino-pretty` for coloured, human-readable output.
 *
 * Usage:
 *   import logger from '@/lib/logger';
 *   logger.info('Hello');
 *   logger.error({ err }, 'Something went wrong');
 */
const logger = pino({
    level: process.env.LOG_LEVEL ?? (isProduction ? 'info' : 'debug'),
    ...(isProduction
        ? {
            // JSON output — one line per entry (Loki-friendly)
            formatters: {
                level(label) {
                    return { level: label };
                },
            },
            timestamp: pino.stdTimeFunctions.isoTime,
        }
        : {
            // Pretty-print in dev
            transport: {
                target: 'pino-pretty',
                options: {
                    colorize: true,
                    translateTime: 'HH:MM:ss.l',
                    ignore: 'pid,hostname',
                },
            },
        }),
});

export default logger;

