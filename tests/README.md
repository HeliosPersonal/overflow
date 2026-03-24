# Overflow — Test Suite

## Structure

```
tests/
├── Overflow.Contracts.UnitTests/        # Pure logic tests for shared contracts (ReputationHelper, VoteTargetType)
├── Overflow.QuestionService.UnitTests/  # Unit tests for command/query handlers, controllers, validators
├── Overflow.VoteService.UnitTests/      # Unit tests for CastVote handler and VotesController
├── Overflow.ProfileService.UnitTests/   # Unit tests for EditProfile, profile queries, controller, reputation handler
└── Overflow.IntegrationTests/           # Happy-path integration tests with real PostgreSQL (Testcontainers)
    ├── QuestionServiceHappyPathTests    # Full lifecycle: create → get → answer → accept → update → delete
    └── EstimationServiceHappyPathTests  # Room lifecycle: create → join → vote → reveal → reset + DB state checks
```

## Running Tests

### Prerequisites

- .NET 10 SDK
- Docker (required for integration tests — Testcontainers spins up PostgreSQL containers)

### Unit tests only (no Docker needed)

```bash
dotnet test --filter "FullyQualifiedName~UnitTests"
```

### Integration tests (requires Docker)

```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

### All tests

```bash
dotnet test
```

## Test Approach

### Unit Tests

- **Framework:** xUnit v3 + Moq + Shouldly
- **Pattern:** AAA (Arrange, Act, Assert)
- **Database:** EF Core InMemory provider for isolation (each test gets a fresh DB)
- **External deps:** All mocked (IMessageBus/Wolverine, IFusionCache, TagService, IHtmlSanitizer)
- **Focus areas:**
    - Command handlers — business logic, validation, error paths, event publishing
    - Query handlers — sorting, filtering, not-found cases
    - Controllers — HTTP status mapping, user claim extraction, delegation to ISender
    - Validators — boundary conditions
    - Contracts — ReputationHelper delta calculations, VoteTargetType validation
- **Skipped tests:** A few handlers use `ExecuteUpdateAsync` or `BeginTransactionAsync` which the InMemory provider
  doesn't support. These are marked with `Skip = "..."` and covered by integration tests.

### Integration Tests

- **Framework:** xUnit v3 + Shouldly + WebApplicationFactory + Testcontainers (PostgreSQL)
- **Scope:** Two focused happy-path test suites:
    1. **QuestionService** — full lifecycle from create through delete, with answer posting and acceptance
    2. **EstimationService** — planning poker room lifecycle: create, join, vote, reveal, reset, with DB state
       verification
- **Auth:** Fake `TestAuthHandler` that reads `X-Test-UserId` / `X-Test-Roles` headers — no Keycloak needed
- **Wolverine:** External transports stubbed via `StubAllExternalTransports()` — no RabbitMQ needed
- **FusionCache:** Replaced with in-memory-only FusionCache (no Redis)
- **Database:** Real PostgreSQL via Testcontainers — schema created with `EnsureCreatedAsync()`
- **Background services:** Disabled (CrossPodBroadcastService, ArchivedRoomCleanupService) to avoid Redis dependency

### Safe Production Refactorings

- `TagService.AreTagsValidAsync()` and `InvalidateCache()` made `virtual` to enable Moq mocking
- `ProfileServiceMarker`, `QuestionServiceMarker`, `EstimationServiceMarker` marker types added to each service's
  `Program.cs` for `WebApplicationFactory<T>`
- `InternalsVisibleTo` added to QuestionService, ProfileService, VoteService, EstimationService csprojs

## CI/CD Integration

Add to `.github/workflows/ci-cd.yml`:

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      # Unit tests — fast, no Docker
      - name: Unit Tests
        run: dotnet test --no-build --filter "FullyQualifiedName~UnitTests" --logger "trx;LogFileName=unit-results.trx"

      # Integration tests — needs Docker (GitHub Actions runners have Docker pre-installed)
      - name: Integration Tests
        run: dotnet test --no-build --filter "FullyQualifiedName~IntegrationTests" --logger "trx;LogFileName=integration-results.trx"

      - name: Publish Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/TestResults/*.trx'
```

> **Tip:** Split unit and integration tests into separate CI jobs if you want
> fast unit test feedback before the slower integration tests run.

## Extending

- **New handler?** Add a test class in the matching `Handlers/` folder. Follow existing patterns.
- **New controller?** Add to `Controllers/` in the matching unit test project.
- **New service?** Create `Overflow.<ServiceName>.UnitTests/` with its own `.csproj`.
- **New integration scenario?** Add to `Overflow.IntegrationTests/` with its own fixture class.
- **Package versions** go in `Directory.Packages.props` only (central package management).
