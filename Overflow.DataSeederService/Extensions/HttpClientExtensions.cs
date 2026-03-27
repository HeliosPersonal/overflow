using Polly;

namespace Overflow.DataSeederService.Extensions;

internal static class HttpClientExtensions
{
    /// <summary>
    /// Adds resilience handler with extended timeouts suitable for cross-namespace K8s calls.
    /// Circuit breaker sampling duration is set to 2x the attempt timeout as required by Polly.
    /// </summary>
    public static IHttpClientBuilder AddDataSeederResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(o =>
        {
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60); // Must be >= 2x AttemptTimeout
            o.Retry.MaxRetryAttempts = 2;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
        });
        return builder;
    }
}

