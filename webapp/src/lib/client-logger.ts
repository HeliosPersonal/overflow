/**
 * Lightweight client-side logger for use in 'use client' components.
 *
 * Wraps `console` methods with a consistent `[module]` prefix.
 * In production builds, debug-level logs are suppressed.
 *
 * For server-side code, use `@/lib/logger` (pino) instead.
 *
 * Usage:
 *   import { createClientLogger } from '@/lib/client-logger';
 *   const log = createClientLogger('LoginPage');
 *   log.info('Login succeeded');
 *   log.error({ err }, 'Something went wrong');
 */

const isProduction = process.env.NODE_ENV === 'production';

interface ClientLogger {
    debug: (...args: unknown[]) => void;
    info: (...args: unknown[]) => void;
    warn: (...args: unknown[]) => void;
    error: (...args: unknown[]) => void;
}

export function createClientLogger(module: string): ClientLogger {
    const prefix = `[${module}]`;

    return {
        debug: (...args: unknown[]) => {
            if (!isProduction) console.debug(prefix, ...args);
        },
        info: (...args: unknown[]) => {
            if (!isProduction) console.info(prefix, ...args);
        },
        warn: (...args: unknown[]) => {
            console.warn(prefix, ...args);
        },
        error: (...args: unknown[]) => {
            console.error(prefix, ...args);
        },
    };
}

