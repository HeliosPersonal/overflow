# Data Seeder Service

A .NET background worker that generates realistic Q&A content for the Overflow staging environment.
It uses [OllamaSharp](https://github.com/awaescher/OllamaSharp) to run a multi-step LLM pipeline and
manages a fixed pool of seeder users in Keycloak.

### Related docs

- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — realm/client setup, audience mappers
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — secrets at runtime
- [Infrastructure](../docs/INFRASTRUCTURE.md) — platform overview

---

## Table of Contents

1. [Overview](#overview)
2. [Jobs](#jobs)
3. [User Pool](#user-pool)
4. [Content Generation Pipeline](#content-generation-pipeline)
5. [Configuration](#configuration)
6. [Project Structure](#project-structure)
7. [Local Development](#local-development)
8. [Troubleshooting](#troubleshooting)

---

## Overview

|                  |                                                                                 |
|------------------|---------------------------------------------------------------------------------|
| **Type**         | .NET 10 Worker Service (3 × `BackgroundService`)                                |
| **Purpose**      | Populate staging with realistic questions, answers, and accepted answers        |
| **LLM**          | [OllamaSharp](https://github.com/awaescher/OllamaSharp) → Ollama (`qwen2.5:3b`) |
| **User pool**    | 20 fixed seeder-prefixed Keycloak users                                         |
| **Environments** | Staging (K8s) and local (Aspire)                                                |

The service is **not** deployed to production.

---

## Jobs

Three independent background jobs share the same `SeederUserPool` singleton.

### PostQuestionJob — every 60 min

```
1. Pick a random seeder user
2. Fetch tags from question-svc, pick one at random
3. Pick random complexity (Beginner / Intermediate / Advanced)
4. Run LLM pipeline: TopicSeed → StructuredQuestion
5. POST question to question-svc
```

### PostAnswerJob — every 15 min

```
1. Fetch the newest question from question-svc
2. Pick a random seeder user who is NOT the question author
3. Pick random complexity
4. Run LLM pipeline: StructuredAnswer → Critic → Repair (if needed)
5. POST answer to question-svc
```

### AcceptBestAnswerJob — every 40 min

```
1. Fetch up to 3 recent unaccepted questions that have at least one answer
2. For each question:
   a. Find the question author in the seeder pool
   b. Ask LLM to pick the best answer (falls back to random if only one answer)
   c. POST accept to question-svc as the question author
```

If the LLM fails at any step, the job logs a warning and skips that iteration — no fallbacks, no retries at the job
level.

---

## User Pool

`SeederUserPool` is a singleton shared across all jobs.

### Lifecycle

1. **Discovery** — searches Keycloak for users whose email starts with `SeederUsernamePrefix` (default `seeder-`).
2. **Creation** — if fewer than `MaxSeederUsers` exist, creates new ones in Keycloak and hits Profile Service to trigger
   profile auto-creation.
3. **Password** — set once at creation using `SeederUserPassword`. Never reset on subsequent runs.
4. **Token refresh** — each job calls `RefreshTokenAsync` per user before acting. Stale tokens are replaced
   transparently.

### Email format

```
seeder-{name}{4-digit suffix}@overflow.local
```

e.g. `seeder-johndoe4521@overflow.local`

> Keycloak must have `registrationEmailAsUsername` enabled — email is the unique identifier.

---

## Content Generation Pipeline

All generation goes through `LlmService`, which uses the OllamaSharp `Chat` class with `format: "json"` — Ollama
guarantees valid JSON output with no markdown fences or prose.

### Steps

```
[Step 1] TopicSeed          (temp 0.7)
         tag + complexity → topic, difficulty, problem_type, bug_reason, key_entities, solution_hint

[Step 2] StructuredQuestion (temp 0.5)
         seed → title, context, code_example, language, expected_behavior, actual_behavior, tags

  └─→ ContentAssembler.BuildQuestionHtml() → POST to question-svc

[Step 3] StructuredAnswer   (temp 0.4)
         question + random style + complexity → explanation, fix_steps, code_snippet, language, notes

[Step 4] Critic             (temp 0.2)
         evaluates answer quality → { valid, issues[] }

[Step 5] Repair             (temp 0.3, only if Critic flags issues)
         fixes flagged issues → { question?, answer? }

  └─→ ContentAssembler.BuildAnswerHtml() → POST to question-svc
```

### Complexity levels

| Level | Label        | Effect                                     |
|-------|--------------|--------------------------------------------|
| 1     | Beginner     | Simple problem, minimal jargon, short code |
| 2     | Intermediate | Standard developer problem                 |
| 3     | Advanced     | Edge cases, performance, deeper concepts   |

### Answer styles

Each answer run picks a random style passed to Step 3:

| Style          | Description               |
|----------------|---------------------------|
| Neutral        | Clear, professional       |
| Conversational | Friendly, approachable    |
| Formal         | Precise, concise          |
| StepByStep     | Numbered steps            |
| CodeHeavy      | Code first, minimal prose |

### Retries

If the LLM returns unparseable JSON, `LlmService` retries up to `MaxGenerationRetries` times (default 2). If all
attempts fail, it returns `null` — the job logs a warning and skips the iteration.

### HTML assembly

`ContentAssembler` wraps the structured DTO fields directly into HTML — no markdown conversion:

- `context` → `<p>`
- `code_example` / `code_snippet` → `<pre><code class="language-{lang}">`
- `fix_steps` → `<ol><li>`
- `expected_behavior` / `actual_behavior` / `notes` → `<h3>` + `<p>`

---

## Configuration

### Base (`appsettings.json`)

All environments inherit these defaults. Override only what differs in environment-specific files.

```json
{
  "SeederOptions": {
    "LlmModel": "qwen2.5:3b",
    "QuestionIntervalMinutes": 60,
    "AnswerIntervalMinutes": 15,
    "AcceptIntervalMinutes": 40,
    "MaxSeederUsers": 20,
    "SeederUsernamePrefix": "seeder-",
    "SeederUserPassword": "...",
    "EnableVoting": true,
    "MaxGenerationRetries": 2,
    "EnableCriticPass": true,
    "EnableRepairPass": true
  },
  "KeycloakOptions": {
    "AdminClientId": "overflow-admin",
    "NextJsClientId": "overflow-web"
  }
}
```

### SeederOptions reference

| Key                       | Default      | Description                                    |
|---------------------------|--------------|------------------------------------------------|
| `LlmApiUrl`               | —            | Ollama base URL, e.g. `http://localhost:11434` |
| `LlmModel`                | `qwen2.5:3b` | Model name passed to Ollama                    |
| `QuestionIntervalMinutes` | 60           | PostQuestionJob cadence                        |
| `AnswerIntervalMinutes`   | 15           | PostAnswerJob cadence                          |
| `AcceptIntervalMinutes`   | 40           | AcceptBestAnswerJob cadence                    |
| `MaxSeederUsers`          | 20           | Pool size                                      |
| `SeederUsernamePrefix`    | `seeder-`    | Email prefix for pool users                    |
| `SeederUserPassword`      | —            | Shared password, set once at user creation     |
| `EnableVoting`            | true         | Cast random votes after posting content        |
| `MaxGenerationRetries`    | 2            | JSON parse retries per pipeline step           |
| `EnableCriticPass`        | true         | Run Step 4 critic after answer generation      |
| `EnableRepairPass`        | true         | Run Step 5 repair when critic flags issues     |

### Environment overrides

| File                           | What it overrides                                             |
|--------------------------------|---------------------------------------------------------------|
| `appsettings.Development.json` | Service URLs (localhost ports), Keycloak URL + secrets        |
| `appsettings.Staging.json`     | Service URLs (k8s names), Keycloak URL + issuers, OTEL config |

---

## Project Structure

```
Overflow.DataSeederService/
├── Program.cs                      # DI, Refit clients, OllamaSharp setup
├── appsettings.json                # Base config
├── appsettings.Development.json    # Local overrides
├── appsettings.Staging.json        # K8s overrides + OTEL
│
├── Jobs/
│   ├── PostQuestionJob.cs          # Every QuestionIntervalMinutes
│   ├── PostAnswerJob.cs            # Every AnswerIntervalMinutes
│   └── AcceptBestAnswerJob.cs      # Every AcceptIntervalMinutes
│
├── Services/
│   ├── SeederUserPool.cs           # Singleton — 20-user pool cache + token refresh
│   ├── UserSyncService.cs          # Creates missing users in Keycloak + Profile Service
│   ├── LlmService.cs               # OllamaSharp Chat, 5-step pipeline
│   ├── QuestionService.cs          # Pipeline → ContentAssembler → question-svc POST
│   ├── AnswerService.cs            # Pipeline → ContentAssembler → question-svc POST
│   ├── ContentAssembler.cs         # DTO fields → HTML; output validation
│   └── VotingService.cs            # Random vote casting
│
├── Clients/
│   ├── IQuestionApiClient.cs       # Refit — question-svc
│   ├── IProfileApiClient.cs        # Refit — profile-svc
│   ├── IVoteApiClient.cs           # Refit — vote-svc
│   ├── IKeycloakAdminClient.cs     # Refit — Keycloak Admin API
│   ├── IKeycloakTokenClient.cs     # Refit — Keycloak token endpoint
│   └── AdminBearerTokenHandler.cs  # Injects admin JWT into Keycloak admin requests
│
├── Keycloak/
│   ├── KeycloakAdminService.cs     # User CRUD via Admin API
│   └── SeederUserService.cs        # Seeder-specific user creation logic
│
├── Models/
│   ├── SeederOptions.cs            # Config POCO
│   ├── Dtos.cs                     # API request/response DTOs
│   ├── LlmGenerationDtos.cs        # Pipeline DTOs (TopicSeedDto, QuestionGenerationDto, …)
│   └── GenerationOptions.cs        # ComplexityLevel, AnswerStyle enums
│
└── Templates/
    └── LlmPrompts.cs               # All 5-step pipeline prompt templates
```

---

## Local Development

### With Aspire (recommended)

```bash
cd Overflow.AppHost
dotnet run
```

Logs and traces are available at **http://localhost:18888**.

### Ollama setup

```bash
ollama serve
ollama pull qwen2.5:3b
```

Set `LlmApiUrl` in `appsettings.Development.json` to `http://localhost:11434`.

### Speed up for local iteration

```json
"QuestionIntervalMinutes": 5,
"AnswerIntervalMinutes": 2,
"AcceptIntervalMinutes": 3,
"EnableCriticPass": false,
"EnableRepairPass": false
```

### Keycloak requirements

- `overflow-admin` — Service Accounts enabled, `realm-admin` role (user creation + password set)
- `overflow-web` — Direct Access Grants enabled (password grant for user tokens)

See [Keycloak Setup](../docs/KEYCLOAK_SETUP.md).

---

## Troubleshooting

### Questions fail after users are created

- Verify at least one tag exists in the Question Service.
- Check Question Service: `kubectl logs -n apps-staging -l app=question-svc -f`

### LLM requests time out or always fail

- First request after pod start can take several minutes (model cold-loading).
- Check Ollama: `kubectl logs -n apps-staging -l app=ollama -f`
- Ensure the model is pulled: `kubectl exec -n apps-staging deploy/ollama -- ollama pull qwen2.5:3b`

### Generation always skipped

- Look for `[TopicSeed]`, `[StructuredQuestion]`, `[StructuredAnswer]` warnings in logs.
- Try increasing `MaxGenerationRetries` to 3.
- Try a larger model — small models sometimes ignore `format: "json"`.

### AcceptBestAnswerJob never accepts

- Questions need at least one answer first.
- The question author must be in the seeder pool — externally-created questions are skipped.

### Users created but tokens fail

- Ensure Direct Access Grants are enabled for `overflow-web` in Keycloak.
- Verify `SeederUserPassword` matches the password set during initial user creation.

### Useful commands

```bash
# Follow seeder logs
kubectl logs -n apps-staging -l app=data-seeder-svc -f

# Restart seeder
kubectl rollout restart deployment/data-seeder-svc -n apps-staging

# Speed up intervals temporarily
kubectl set env deployment/data-seeder-svc -n apps-staging \
  SeederOptions__QuestionIntervalMinutes=10 \
  SeederOptions__AnswerIntervalMinutes=5 \
  SeederOptions__AcceptIntervalMinutes=8

# Disable critic + repair (faster generation)
kubectl set env deployment/data-seeder-svc -n apps-staging \
  SeederOptions__EnableCriticPass=false \
  SeederOptions__EnableRepairPass=false

# Pull model into Ollama pod
kubectl exec -n apps-staging deploy/ollama -- ollama pull qwen2.5:3b
```

---

## Possible Improvements

- **Add a seeding progress dashboard endpoint** — Expose a simple `/status` HTTP endpoint (or health check detail) that
  reports the current state of the seeder: number of questions/answers generated, last run timestamps per job, LLM
  failure rate, and pool readiness. This would simplify monitoring without digging through logs.
- **Support pluggable LLM backends** — Currently the service is tightly coupled to Ollama via OllamaSharp. Abstracting
  the LLM interaction behind an `ILlmClient` interface would allow swapping in OpenAI-compatible APIs (e.g., Azure
  OpenAI, Anthropic) for faster or higher-quality generation without changing the pipeline logic.
- **Add content diversity tracking** — Track which tags, complexity levels, and answer styles have been used recently
  and bias the random selection toward underrepresented combinations. This prevents the seeder from repeatedly
  generating similar content and ensures staging data covers a broader range of topics.
