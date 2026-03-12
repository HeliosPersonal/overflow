/**
 * Next.js Instrumentation Hook
 *
 * Next.js 15+ supports a "instrumentation.node.ts" pattern:
 * When NEXT_RUNTIME === 'nodejs', we dynamically import the Node.js-only module.
 * The dynamic import ensures the Edge bundler never statically analyses
 * instrumentation.node.ts and never complains about Node.js-only APIs
 * (process.cwd, path, process.on, dotenv, etc.).
 *
 * @see https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */
export async function register() {
    if (process.env.NEXT_RUNTIME === 'nodejs') {
        const { register } = await import('./instrumentation.node');
        await register();
    }
}
