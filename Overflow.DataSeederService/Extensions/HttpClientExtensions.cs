using Polly;

namespace Overflow.DataSeederService.Extensions;

internal static class HttpClientExtensions
{
    public static IHttpClientBuilder AddDataSeederResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(o =>
        {
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            o.Retry.MaxRetryAttempts = 2;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
        });
        return builder;
    }
}