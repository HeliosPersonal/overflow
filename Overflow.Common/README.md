# Overflow.Common

Shared extensions and helpers used by all backend services.

---

## What's Inside

### CommonExtensions/

| Extension                         | Purpose                                                                                    |
|-----------------------------------|--------------------------------------------------------------------------------------------|
| `WebApplicationBuilderExtensions` | `AddEnvVariablesAndConfigureSecrets()` — loads env vars + Infisical secrets (staging/prod) |
| `KeycloakConfigurationExtensions` | `ConfigureKeycloakFromSettings()` — validates Keycloak config section exists               |
| `AuthExtensions`                  | `AddKeyCloakAuthentication()` — configures JWT Bearer auth against Keycloak                |
| `WolverineExtensions`             | `UseWolverineWithRabbitMqAsync()` — sets up Wolverine + RabbitMQ transport                 |
| `HostExtensions`                  | `MigrateDbContextAsync<T>()` — runs EF Core migrations at startup                          |
| `FusionCacheExtensions`           | `AddFusionCacheWithRedis()` — registers FusionCache with Redis L2 + backplane              |
| `HealthCheckExtensions`           | `AddRabbitMqHealthCheck()`, `AddTypesenseHealthCheck()`, `AddDatabaseHealthCheck<T>()`     |

### Health/

| Class                    | Purpose                                    |
|--------------------------|--------------------------------------------|
| `DatabaseHealthCheck<T>` | EF Core database connectivity health check |
| `RabbitMqHealthCheck`    | RabbitMQ connectivity health check         |
| `TypesenseHealthCheck`   | Typesense connectivity health check        |

### Options/

| Class                     | Purpose                                                             |
|---------------------------|---------------------------------------------------------------------|
| `KeycloakOptions`         | Keycloak URL, realm, audience, valid issuers, admin client settings |
| `DistributedCacheOptions` | FusionCache configuration (Redis connection, durations, fail-safe)  |

### Constants

| Class               | Purpose                                                 |
|---------------------|---------------------------------------------------------|
| `CacheKeys`         | Well-known cache keys used across services              |
| `CacheTags`         | Well-known cache tags for bulk FusionCache invalidation |
| `ConfigurationKeys` | Well-known `IConfiguration` keys used across services   |

### Pagination

`PaginationResult<T>` and `PaginationRequest` — shared pagination helpers with a max page size capped at 50.

---

## Usage Pattern

Every service `Program.cs` starts with:

```csharp
builder.AddEnvVariablesAndConfigureSecrets(); // Infisical in staging/prod; env vars only in dev
builder.ConfigureKeycloakFromSettings();      // Only services that validate JWTs
builder.AddServiceDefaults();                 // OTel, health, service discovery
builder.AddKeyCloakAuthentication();          // JWT bearer from Keycloak
```

---


## Related Documentation

- [Infisical Setup](../docs/INFISICAL_SETUP.md) — How secrets flow from Infisical to services
- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — JWT validation and realm configuration
