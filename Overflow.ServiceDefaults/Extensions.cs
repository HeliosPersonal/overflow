using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.Json;
using Npgsql;

namespace Overflow.ServiceDefaults;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Project OTLP environment variables from config to top-level for OpenTelemetry SDK
        builder.ProjectOtlpEnvironmentVariables();
        
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();

            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                var serviceVersion = typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";
                var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;

                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion);

                // Add standard resource attributes
                var attributes = new Dictionary<string, object>
                {
                    ["host.name"] = Environment.MachineName,
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                };

                resource.AddAttributes(attributes);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddNpgsqlInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.client_ip", httpRequest.HttpContext.Connection.RemoteIpAddress);
                            activity.SetTag("http.request_content_length", httpRequest.ContentLength);
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response_content_length", httpResponse.ContentLength);
                        };
                        // Exclude health check requests from tracing
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.request.method", httpRequestMessage.Method.Method);
                        };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.statement", command.CommandText);
                            activity.SetTag("db.operation", command.CommandType.ToString());
                        };
                    });
            })
            .UseOtlpExporter(); // This reads OTEL_EXPORTER_OTLP_* from configuration

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Liveness probe - K8s uses this to determine if pod should be restarted
        // Fast check, no dependencies, just validates app is responsive
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            AllowCachingResponses = false
        });

        // Readiness probe - K8s uses this to determine if pod should receive traffic
        // Checks all critical dependencies (DB, messaging, cache, etc.)
        app.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            AllowCachingResponses = false,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds,
                        exception = e.Value.Exception?.Message
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                };

                context.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
                await context.Response.WriteAsync(JsonSerializer.Serialize(result, jsonOptions));
            }
        });

        // Full health status - For monitoring, debugging, and comprehensive health view
        // Includes all registered health checks with detailed information
        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
        {
            AllowCachingResponses = false,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    service = context.Request.Host.Host,
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds,
                        tags = e.Value.Tags.ToArray(),
                        data = e.Value.Data,
                        exception = e.Value.Exception?.Message
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                };

                context.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
                await context.Response.WriteAsync(JsonSerializer.Serialize(result, jsonOptions));
            }
        });

        return app;
    }
    
    /// <summary>
    /// Projects environment variables from appsettings EnvironmentVariables:Values section to top-level configuration.
    /// This is required for OpenTelemetry's UseOtlpExporter() which reads OTEL_EXPORTER_OTLP_* from top-level config.
    /// 
    /// Note: Values already present at top-level (e.g., from actual environment variables or Infisical secrets)
    /// take precedence and will NOT be overridden by appsettings values.
    /// This allows Infisical to inject sensitive values like OTEL_EXPORTER_OTLP_HEADERS.
    /// </summary>
    private static void ProjectOtlpEnvironmentVariables(this IHostApplicationBuilder builder)
    {
        // Source: appsettings EnvironmentVariables:Values:{KEY} = {VALUE}
        // Target: top-level config {KEY} = {VALUE} (e.g., OTEL_EXPORTER_OTLP_ENDPOINT)
        // Priority: Actual env vars / Infisical secrets > appsettings values

        var pairs = builder.Configuration
            .GetSection("EnvironmentVariables:Values")
            .GetChildren();

        var overlay = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in pairs)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null)
            {
                continue;
            }

            // Only add if not already present at top-level
            // This ensures environment variables and Infisical secrets take precedence over appsettings
            if (string.IsNullOrEmpty(builder.Configuration[kv.Key]))
            {
                overlay[kv.Key] = kv.Value;
            }
        }

        if (overlay.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(overlay);
        }
    }
}