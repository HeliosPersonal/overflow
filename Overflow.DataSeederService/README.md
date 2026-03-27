# AI Answer Service (Data Seeder)

Event-driven .NET service that generates AI-powered answers for user questions.
Consumes `QuestionCreated` events via RabbitMQ, generates answers using
[OllamaSharp](https://github.com/awaescher/OllamaSharp), and posts them as a dedicated AI user account.

### Related docs

- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — realm/client setup, audience mappers
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — secrets at runtime
- [Infrastructure](../docs/INFRASTRUCTURE.md) — platform overview

---

## Overview

|                  |                                                                       |
|------------------|-----------------------------------------------------------------------|
| **Type**         | .NET 10 Web App (Wolverine message handler)                           |
| **Purpose**      | Automatically answer user questions with AI-generated content         |
| **LLM**          | OllamaSharp → Ollama (model configured via `LlmModel`)                |
| **AI User**      | Single Keycloak account (display name configured via `AiDisplayName`) |
| **Messaging**    | RabbitMQ via Wolverine — consumes `QuestionCreated` events            |
| **Result**       | Uses `CSharpFunctionalExtensions.Result<T>` throughout all services   |
| **Environments** | Staging (K8s) and local (Aspire). **Not** deployed to production.     |

---

## How It Works

```
User asks a question
  → QuestionService publishes QuestionCreated to RabbitMQ
    → QuestionCreatedHandler receives the event
      → AiAnswerService orchestrates the pipeline:
          1. AiUserProvider — ensure AI user is bootstrapped
          2. LlmService    — generate N variants, pick the best
          3. Post answer to QuestionService via HTTP
      → On failure: Result.Failure → handler throws → Wolverine retries → DLQ
```

---

## AI User

Bootstrap is **best-effort** — if Keycloak is unreachable the service starts anyway.

**Startup (AiUserBootstrapService):**

1. Validates `AiEmail` / `AiPassword` are configured (logs critical and skips if empty)
2. Attempts up to 3 retries with exponential backoff
3. If all retries fail, logs an error — bootstrap will be retried lazily on the first event

**Lazy bootstrap (AiUserProvider):**

- If startup bootstrap failed, `GetUserAsync()` attempts one lazy bootstrap on the first `QuestionCreated` message
- Thread-safe via `SemaphoreSlim`

**Bootstrap steps:**

1. Creates (or finds) a Keycloak account via Admin API
2. Authenticates via password grant to get a JWT
3. Calls `GET /profiles/me` to trigger profile auto-creation (best-effort)

**LLM model** is checked/pulled lazily on the first LLM call, not at startup.

---

## Answer Generation

For each `QuestionCreated` event:

1. **Generate N variants** — each is an independent LLM call producing structured JSON (explanation, fix steps, code
   snippet, notes)
2. **Validate + render** — `AnswerHtmlRenderer` checks for non-empty fields, reasonable code length, minimum HTML length
3. **Pick the best** — if multiple valid variants exist, asks the LLM to rank them
4. **Post** — sends the winning answer to QuestionService via HTTP

If all variants fail validation, the handler throws and Wolverine retries. After max retries the message moves to the
dead-letter queue.

---

## Configuration

### `AiAnswerOptions` (in `appsettings.json`)

All numeric options are **required** — no defaults. The service fails fast at startup if any is missing.

| Key                        | Description                                                                       |
|----------------------------|-----------------------------------------------------------------------------------|
| `QuestionServiceUrl`       | Base URL for posting answers                                                      |
| `ProfileServiceUrl`        | Base URL for profile auto-creation                                                |
| `LlmApiUrl`                | Ollama API endpoint                                                               |
| `LlmModel`                 | Ollama model name                                                                 |
| `AiDisplayName`            | Display name for the AI user                                                      |
| `AiEmail`                  | Keycloak email (Infisical: `AI_ANSWER_OPTIONS__AI_EMAIL`). Empty = disabled       |
| `AiPassword`               | Keycloak password (Infisical: `AI_ANSWER_OPTIONS__AI_PASSWORD`). Empty = disabled |
| `AnswerVariants`           | Number of answer variants to generate                                             |
| `MaxGenerationRetries`     | Max LLM retries per variant                                                       |
| `LlmTimeoutSeconds`        | HTTP client timeout for the Ollama HttpClient                                     |
| `GenerationTimeoutSeconds` | Per-attempt timeout for answer generation LLM calls                               |
| `RankingTimeoutSeconds`    | Timeout for the variant ranking LLM call                                          |

### `KeycloakOptions`

Standard Keycloak configuration — see other services for reference.

---

## Project Structure

```
Overflow.DataSeederService/
  Program.cs                              — Entry point, wires up DI via extension methods
  AiUserBootstrapService.cs               — Hosted service: best-effort AI user bootstrap on startup
  Clients/
    AdminBearerTokenHandler.cs            — AsyncLocal-based admin token injection for Keycloak Admin API
    IKeycloakAdminClient.cs               — Refit: Keycloak Admin REST API + request/response DTOs
    IKeycloakTokenClient.cs               — Refit: Keycloak token endpoint + grant DTOs
    IProfileApiClient.cs                  — Refit: ProfileService
    IQuestionApiClient.cs                 — Refit: QuestionService
  Extensions/
    HttpClientExtensions.cs               — Resilience handler with extended timeouts for K8s calls
    ServiceCollectionExtensions.cs        — DI registration: Ollama, Keycloak, API clients, app services
  Keycloak/
    KeycloakAdminService.cs               — Keycloak admin operations with Result<T> pattern
  MessageHandlers/
    QuestionCreatedHandler.cs             — Wolverine handler: throws on failure → retry → DLQ
  Models/
    AiAnswerOptions.cs                    — Configuration (all numeric fields required, no defaults)
    Dtos.cs                               — CreateAnswerDto, Answer, AiUser record
    LlmGenerationDtos.cs                  — AnswerGenerationDto, AnswerWithScore record
  Services/
    AiAnswerService.cs                    — Orchestrates: user → LLM → token → post (Result<Answer>)
    AiUserProvider.cs                     — Singleton: bootstrap + lazy retry + token refresh
    AnswerHtmlRenderer.cs                 — Static: validation, HTML rendering, language normalisation
    LlmService.cs                         — LLM: generate variants, rank, model management
```

---

## Local Development

```bash
# Start all services via Aspire (includes Ollama, Keycloak, RabbitMQ)
cd Overflow.AppHost && dotnet run
```

The service starts automatically via Aspire. It will:

1. Wait for Keycloak, RabbitMQ, QuestionService, ProfileService, and Ollama to be ready
2. Bootstrap the AI user in Keycloak (best-effort — service starts even if Keycloak is slow)
3. Begin listening for `QuestionCreated` events

Credentials for local dev are in `appsettings.Development.json` (committed).

To test: post a question via the webapp at `http://localhost:3000` — the AI answer should appear
within seconds to minutes depending on LLM speed.
