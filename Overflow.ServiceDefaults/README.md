# Overflow.ServiceDefaults

Shared Aspire service defaults — OpenTelemetry, health endpoints, service discovery, and HTTP resilience.

---

## What's Inside

Provides `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`:

| Feature               | Description                                          |
|-----------------------|------------------------------------------------------|
| **OpenTelemetry**     | Traces, metrics, and logs exported via OTLP          |
| **Health endpoints**  | `/health` (readiness) and `/alive` (liveness)        |
| **Service discovery** | Aspire service discovery for local dev               |
| **HTTP resilience**   | Default retry and circuit breaker policies via Polly |

---

## Usage

```csharp
builder.AddServiceDefaults();
// ... build app ...
app.MapDefaultEndpoints();
```

Every backend service and the webapp reference this project.

---

## Possible Improvements

- **Add custom OpenTelemetry metrics for business events** — Beyond default runtime metrics, add custom counters and
  histograms (e.g., questions created/sec, search latency P99, votes cast/min) using the `System.Diagnostics.Metrics`
  API. These business-level metrics would make Grafana dashboards more actionable.
- **Add configurable HTTP resilience profiles** — Currently all services share the same default retry/circuit-breaker
  policy. Adding named profiles (e.g., `"aggressive"` with more retries for critical paths, `"lenient"` for best-effort
  calls) would let each service tune resilience to its specific inter-service communication patterns.
