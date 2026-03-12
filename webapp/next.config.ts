import type { NextConfig } from "next";

const nextConfig: NextConfig = {
    output: 'standalone',
    logging: {
        fetches: {
            fullUrl: true
        }
    },
    // Keep OTEL and Infisical packages in Node.js require() context, not bundled by webpack.
    // Bundling them causes TransformStream / fetch monkey-patch conflicts with Next.js 16 internals.
    serverExternalPackages: [
        '@opentelemetry/sdk-node',
        '@opentelemetry/auto-instrumentations-node',
        '@opentelemetry/exporter-trace-otlp-http',
        '@opentelemetry/exporter-metrics-otlp-http',
        '@opentelemetry/sdk-metrics',
        '@opentelemetry/resources',
        '@opentelemetry/semantic-conventions',
        '@infisical/sdk',
    ],
};

export default nextConfig;
