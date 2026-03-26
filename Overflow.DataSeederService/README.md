# AI Answer Service (Data Seeder)

An event-driven .NET service that generates AI-powered answers for user questions.
When a user posts a question, this service receives a `QuestionCreated` event via RabbitMQ,
generates an answer using [OllamaSharp](https://github.com/awaescher/OllamaSharp), and posts it
as a dedicated AI user account.

### Related docs

- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — realm/client setup, audience mappers
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — secrets at runtime
- [Infrastructure](../docs/INFRASTRUCTURE.md) — platform overview

---

## Table of Contents

1. [Overview](#overview)
2. [How It Works](#how-it-works)
3. [AI User](#ai-user)
4. [Answer Generation](#answer-generation)
5. [Configuration](#configuration)
6. [Project Structure](#project-structure)
7. [Local Development](#local-development)

---

## Overview

|                  |                                                                                 |
|------------------|---------------------------------------------------------------------------------|
| **Type**         | .NET 10 Web App (Wolverine message handler)                                     |
| **Purpose**      | Automatically answer user questions with AI-generated content                   |
| **LLM**          | [OllamaSharp](https://github.com/awaescher/OllamaSharp) → Ollama (`qwen2.5:3b`) |
| **AI User**      | Single Keycloak account (`AI Assistant`)                                        |
| **Messaging**    | RabbitMQ via Wolverine — consumes `QuestionCreated` events                      |
| **Environments** | Staging (K8s) and local (Aspire)                                                |

The service is **not** deployed to production.

---

## How It Works

```
User asks a question
  → QuestionService publishes QuestionCreated event to RabbitMQ
    → DataSeederService receives the event (Wolverine handler)
      → LLM generates 3 answer variants
        → LLM picks the best variant
          → Posts the answer to QuestionService as "AI Assistant"
```

The entire flow is **event-driven** — no polling, no timers. The AI responds as soon as the
LLM finishes generating (typically 10-60 seconds depending on the model and hardware).

---

## AI User

On startup, the service:

1. Creates (or finds) a Keycloak account: `ai-assistant@overflow.local`
2. Authenticates to get a JWT token
3. Calls `GET /profiles/me` to trigger profile auto-creation in ProfileService

This AI user appears in the frontend like any other user — with the display name **"AI Assistant"**.

---

## Answer Generation

For each `QuestionCreated` event:

1. **Generate N variants** (default: 3) — each is an independent LLM call that produces a structured JSON answer with
   explanation, fix steps, code snippet, and notes.
2. **Validate** each variant — checks for non-empty fields, reasonable code length.
3. **Render to HTML** — converts the structured answer to HTML with proper code blocks.
4. **Pick the best** — if multiple valid variants exist, asks the LLM to rank them and select the most correct/helpful
   one.
5. **Post** — sends the winning answer to QuestionService via HTTP as the AI user.

If all variants fail validation, the answer is skipped with a warning log.

---

## Configuration

### `AiAnswerOptions` (in `appsettings.json`)

| Key                    | Default                       | Description                           |
|------------------------|-------------------------------|---------------------------------------|
| `QuestionServiceUrl`   | `http://localhost:5000`       | Base URL for posting answers          |
| `ProfileServiceUrl`    | `http://localhost:5002`       | Base URL for profile auto-creation    |
| `LlmApiUrl`            | `http://localhost:11434`      | Ollama API endpoint                   |
| `LlmModel`             | `qwen2.5:3b`                  | Ollama model name                     |
| `AiDisplayName`        | `AI Assistant`                | Display name for the AI user          |
| `AiEmail`              | (from Infisical/config)       | Keycloak email for the AI user (`ai-assistant@staging.overflow.dev` in staging, `ai-assistant@overflow.local` locally) |
| `AiPassword`           | (from Infisical/config)       | Keycloak password for the AI user     |
| `AnswerVariants`       | `3`                           | Number of answer variants to generate |

**Staging/Production:** `AiEmail` and `AiPassword` must be set in **Infisical** under `/app/services`:
- `AI_ANSWER_OPTIONS__AI_EMAIL` (e.g., `ai-assistant@overflow.local`)
- `AI_ANSWER_OPTIONS__AI_PASSWORD` (secure password for the Keycloak AI user)

**Local Development:** Set them as environment variables or in `appsettings.Development.json` (not committed).
| `MaxGenerationRetries` | `2`                           | Max LLM retries per variant           |

### `KeycloakOptions`

Standard Keycloak configuration — see other services for reference.

---

## Project Structure

```
Overflow.DataSeederService/
  Program.cs                           — Service entry point (Wolverine + RabbitMQ setup)
  AiUserBootstrapService.cs            — Hosted service: ensures AI user exists on startup
  Clients/
    AdminBearerTokenHandler.cs         — Injects admin token into Keycloak Admin API calls
    IKeycloakAdminClient.cs            — Refit client: Keycloak Admin REST API
    IKeycloakTokenClient.cs            — Refit client: Keycloak token endpoint
    IProfileApiClient.cs               — Refit client: ProfileService (profile auto-creation)
    IQuestionApiClient.cs              — Refit client: QuestionService (post answers)
  Keycloak/
    KeycloakAdminService.cs            — Keycloak admin operations (create user, get token)
  MessageHandlers/
    QuestionCreatedHandler.cs          — Wolverine handler: QuestionCreated → AI answer
  Models/
    AiAnswerOptions.cs                 — Configuration options
    Dtos.cs                            — Request/response DTOs + AiUser model
    LlmGenerationDtos.cs              — LLM structured output DTOs
  Services/
    AiAnswerService.cs                 — Orchestrates answer generation + posting
    AiUserProvider.cs                  — Singleton: manages AI user lifecycle + token refresh
    LlmService.cs                      — LLM interaction: generate variants, rank, render HTML
```

---

## Local Development

```bash
# Start all services via Aspire (includes Ollama, Keycloak, RabbitMQ)
cd Overflow.AppHost && dotnet run
```

The service starts automatically via Aspire. It will:

1. Wait for Keycloak, RabbitMQ, QuestionService, ProfileService, and Ollama to be ready
2. Bootstrap the AI user in Keycloak
3. Begin listening for `QuestionCreated` events

To test: post a question via the webapp at `http://localhost:3000` — the AI answer should appear within seconds to
minutes (depending on LLM speed).
