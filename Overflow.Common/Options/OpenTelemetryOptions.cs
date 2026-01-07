namespace Overflow.Common.Options;

public class OpenTelemetryOptions
{
    /// <summary>
    /// OTLP endpoint URL (e.g., https://otlp-gateway-prod-eu-west-2.grafana.net/otlp)
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// OTLP protocol (e.g., http/protobuf, grpc)
    /// </summary>
    public required string Protocol { get; set; }

    /// <summary>
    /// Service name for telemetry identification
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Authentication headers (stored in Infisical as OpenTelemetry__Headers)
    /// Format: Authorization=Basic [base64-encoded-credentials]
    /// </summary>
    public required string? Headers { get; set; } =
        "Authorization=Basic MTQ4ODA2MDpnbGNfZXlKdklqb2lNVFl6TkRjeU1DSXNJbTRpT2lKemRHRmpheTB4TkRnNE1EWXdMVzkwYkhBdGQzSnBkR1V0YjNabGNtWnNiM2N0WjNKaFptRnVZU0lzSW1zaU9pSmFXbXN3ZWpadE1XbzBZemN4VkZoUWFrcFdPRXN6TVRFaUxDSnRJanA3SW5JaU9pSndjbTlrTFdWMUxYZGxjM1F0TWlKOWZRPT0=";

    /// <summary>
    /// Resource attributes to add to all telemetry
    /// </summary>
    public required Dictionary<string, string> ResourceAttributes { get; set; }
}