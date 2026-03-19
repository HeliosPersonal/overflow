# Overflow.Common

Shared extensions and helpers used by all backend services.

---

## What's Inside

### CommonExtensions/

| Extension                         | Purpose                                                                                    |
|-----------------------------------|--------------------------------------------------------------------------------------------|
| `WebApplicationBuilderExtensions` | `AddEnvVariablesAndConfigureSecrets()` — loads env vars + Infisical secrets (staging/prod) |
| `KeycloakExtensions`              | `ConfigureKeycloakFromSettings()` — binds `KeycloakOptions` from config                    |
| `AuthExtensions`                  | `AddKeyCloakAuthentication()` — configures JWT Bearer auth against Keycloak                |
| `WolverineExtensions`             | `UseWolverineWithRabbitMqAsync()` — sets up Wolverine + RabbitMQ transport                 |
| `MigrationExtensions`             | `MigrateDbContextAsync<T>()` — runs EF Core migrations at startup                          |
| `DbContextExtensions`             | `AddNpgsqlDbContext<T>()` — registers a Npgsql DbContext from connection string            |

### Health/

| Class                    | Purpose                                    |
|--------------------------|--------------------------------------------|
| `DatabaseHealthCheck<T>` | EF Core database connectivity health check |
| `RabbitMqHealthCheck`    | RabbitMQ connectivity health check         |
| `TypesenseHealthCheck`   | Typesense connectivity health check        |

### Options/

| Class              | Purpose                                                             |
|--------------------|---------------------------------------------------------------------|
| `KeycloakOptions`  | Keycloak URL, realm, audience, valid issuers, admin client settings |
| `InfisicalOptions` | Infisical client ID, secret, project ID                             |

### Pagination

`PaginationResult<T>` and `PaginationRequest` — shared pagination helpers with a max page size capped at 50.

---

## Usage Pattern

Every service `Program.cs` starts with:

```csharp
builder.AddEnvVariablesAndConfigureSecrets(); // Infisical in staging/prod; env vars only in dev
builder.ConfigureKeycloakFromSettings();
builder.AddServiceDefaults();                 // OTel, health, service discovery
builder.AddKeyCloakAuthentication();          // JWT bearer from Keycloak
```

---

## Possible Improvements

- **Add a circuit breaker for Infisical SDK calls** — If the Infisical service is unreachable at startup,
  `AddEnvVariablesAndConfigureSecrets()` currently blocks. Adding a Polly circuit breaker with a fallback to cached
  secrets or environment variables would improve startup resilience in degraded infrastructure scenarios.
- **Extract health checks into a NuGet-style shared package** — The custom health checks (`DatabaseHealthCheck<T>`,
  `RabbitMqHealthCheck`, `TypesenseHealthCheck`) are useful across projects. Packaging them as a reusable internal
  library with generic registration (`builder.Services.AddOverflowHealthChecks()`) would reduce boilerplate in each
  service's `Program.cs`.
- **Add structured logging context enrichment** — Create a middleware or extension that automatically enriches all log
  entries with common context (service name, trace ID, user ID). This would improve log correlation in Grafana without
  requiring each service to manually include these fields.

---

## Related Documentation

- [Infisical Setup](../docs/INFISICAL_SETUP.md) — How secrets flow from Infisical to services
- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — JWT validation and realm configuration
