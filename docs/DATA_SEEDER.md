# Overflow — Data Seeder Service

The Data Seeder Service is a .NET background worker that automatically generates realistic Q&A content
for the Overflow platform. It uses an LLM (via an OpenAI-compatible API) to produce varied questions and
answers, manages a fixed pool of seeder users in Keycloak, and simulates voting — all without manual
intervention.

### Related Documentation

- [Quick Start Guide](./QUICKSTART.md) — Local and Kubernetes setup
- [Infrastructure Documentation](./INFRASTRUCTURE.md) — Platform architecture
- [Keycloak Setup](./KEYCLOAK_SETUP.md) — Realm/client setup, audience mappers
- [Infisical Secret Management](./INFISICAL_SETUP.md) — Secrets flow

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Seeding Cycle](#seeding-cycle)
4. [User Pool Management](#user-pool-management)
5. [Content Variability](#content-variability)
6. [LLM Integration](#llm-integration)
7. [Configuration](#configuration)
8. [Project Structure](#project-structure)
9. [Local Development](#local-development)
10. [Kubernetes Deployment](#kubernetes-deployment)
11. [Troubleshooting](#troubleshooting)

---

## Overview

| | |
|---|---|
| **Type** | .NET Worker Service (BackgroundService) |
| **Framework** | .NET 10 |
| **Purpose** | Generate realistic seed data (users, questions, answers, votes) |
| **Runs in** | Staging environment (also available locally via Aspire) |
| **LLM Backend** | Any OpenAI-compatible API (Ollama, llama.cpp, etc.) |
| **User Pool** | Fixed pool of 20 seeder-prefixed Keycloak users |

The service is **not** deployed to production — it exists to populate the staging environment
with realistic content for demos, testing, and frontend development.

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│           Data Seeder Service                   │
│         (BackgroundService loop)                │
│                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │  User     │  │ Question │  │ Answer   │      │
│  │ Generator │  │Generator │  │Generator │      │
│  └─────┬─────┘  └─────┬────┘  └────┬─────┘      │
│        │              │             │            │
│  ┌─────┴─────┐  ┌─────┴────┐  ┌────┴─────┐      │
│  │  Auth     │  │  LLM     │  │ Voting   │      │
│  │ Service   │  │  Client  │  │ Service  │      │
│  └─────┬─────┘  └─────┬────┘  └────┬─────┘      │
└────────┼──────────────┼────────────┼─────────────┘
         │              │            │
    ┌────▼────┐   ┌─────▼────┐  ┌───▼────────┐
    │Keycloak │   │ Ollama / │  │ question-  │
    │ Admin   │   │ LLM API  │  │ svc / vote │
    │  API    │   │          │  │  -svc      │
    └─────────┘   └──────────┘  └────────────┘
```

The seeder talks to:

- **Keycloak** — creates and re-authenticates seeder users (Admin API + token endpoint)
- **Profile Service** — triggers profile auto-creation via middleware
- **Question Service** — creates questions and answers
- **Vote Service** — casts random votes on content
- **LLM API** — generates question titles, content, answers, and selects best answers

---

## Seeding Cycle

Each cycle runs on a configurable interval (`IntervalMinutes`) and follows these steps:

```
Step 1  │  Manage User Pool
        │  → Discover existing seeder-* users in Keycloak
        │  → Rehydrate them (reset password + get fresh token)
        │  → Create new users if under the 20-user limit
        │
Step 2  │  Select Random Asker
        │  → Pick one user from the pool, refresh their token
        │
Step 3  │  Generate Question
        │  → Fetch available tags from question-svc
        │  → Select 1-3 random tags
        │  → Generate title + content in a single LLM call (ensures topic consistency)
        │  → Fallback: separate title/content calls, then static templates
        │  → POST to question-svc
        │
Step 4  │  Realistic Delay (5-15 seconds)
        │
Step 5  │  Generate Answers
        │  → Pick 2-5 random answerers (excluding the asker)
        │  → Generate answers via LLM (or fallback to templates)
        │  → POST each answer to question-svc with realistic delays
        │
Step 6  │  Select Best Answer
        │  → Use LLM to evaluate and pick the best answer
        │  → Or random selection if LLM is disabled
        │
Step 7  │  Accept Answer
        │  → Asker accepts the best answer
        │
Step 8  │  Random Votes
        │  → Remaining users vote on the question and answers
        │  → 70% upvote / 30% downvote for questions
        │  → 80% upvote / 20% downvote for answers
```

---

## User Pool Management

The seeder maintains a **fixed pool of 20 users** identified by a `seeder-` username prefix.
This ensures the seeder doesn't pollute the system with hundreds of users while still having
enough diversity for realistic interactions.

### How It Works

1. **Discovery** — On each cycle, the seeder searches Keycloak for existing users whose username
   starts with `seeder-` (via the Admin API).

2. **Rehydration** — For each found user, the service resets their password (via Keycloak Admin API)
   and obtains a fresh JWT token. This makes the service restart-safe — no in-memory state is
   required to persist between restarts.

3. **Creation** — If fewer than `MaxSeederUsers` (default: 20) users exist, new ones are created to
   fill the gap. Each new user is registered in Keycloak and their profile is auto-created by hitting
   the Profile Service (which triggers the `UserProfileCreationMiddleware`).

4. **Isolation** — Only `seeder-*` prefixed users are touched. Real users and users created through
   the frontend are never affected.

### Username Format

```
seeder-{namePart}{randomSuffix}
```

Example: `seeder-johndoe4521`, `seeder-janesmith8734`

---

## Content Variability

A key design goal is that generated content should be **diverse** — varying in length, depth,
complexity, and style. This is achieved through a `ContentVariability` system that randomizes
generation parameters for each piece of content.

### Variability Dimensions

| Dimension | Values | Affects |
|---|---|---|
| **Length** | Short, Medium, Long | Sentence count, max_tokens, code block count |
| **Depth** | Beginner, Intermediate, Expert | Persona, terminology, assumed knowledge |
| **Complexity** | Simple, Moderate, Complex | Question structure, problem scope |
| **Style** (answers) | Neutral, Conversational, Formal, ProsAndCons, StepByStep, CodeHeavy, Opinionated | Tone, structure, formatting |

### Examples

| Variability | Question Example | Answer Example |
|---|---|---|
| Short + Beginner + Simple | "How do I convert a string to int?" (2-3 sentences) | One-liner with `int.TryParse()` |
| Medium + Intermediate | Specific scenario with code block, expected vs actual behavior | Step-by-step solution with explanation |
| Long + Expert + Complex | Multi-layered debugging scenario with environment details, profiling results, and attempted fixes | Comprehensive analysis with alternatives, trade-offs, and code |

### Where Variability Lives

All prompt templates and variability logic are centralized in the `Templates/` folder:

| File | Purpose |
|---|---|
| `LlmPrompts.cs` | Builds system + user prompts for the LLM with randomized variability |
| `QuestionTemplates.cs` | Fallback question titles and content (short/medium/long per tag) |
| `AnswerTemplates.cs` | Fallback answers organized by style (7 categories, 25+ templates) |

The `LlmClient` itself contains **no prompt strings** — it only handles HTTP communication.

### Topic Consistency

Questions are generated using a **unified title+body** LLM call — both the title and the body
are produced in a single request using a `===TITLE===` / `===BODY===` separator format. This
guarantees that the body directly elaborates on the title's topic (e.g., a title about exception
handling will always have a body about exception handling).

If the unified call fails, the service falls back to separate title → content calls, and
ultimately to static templates.

---

## LLM Integration

The service communicates with any OpenAI-compatible chat completions API.

### Supported Backends

| Backend | URL Format | Notes |
|---|---|---|
| **Ollama** | `http://host:11434/v1/chat/completions` | Used in staging (K8s) |
| **llama.cpp** | `http://host:12434/engines/llama.cpp/v1/chat/completions` | Used locally |
| **OpenAI** | `https://api.openai.com/v1/chat/completions` | Works but costs money |
| **Any compatible** | Varies | Must support `/v1/chat/completions` endpoint |

### Request Format

```json
{
  "model": "phi3.5",
  "messages": [
    { "role": "system", "content": "..." },
    { "role": "user", "content": "..." }
  ],
  "temperature": 0.7,
  "max_tokens": 500
}
```

### Timeout & Resilience

LLM requests can take minutes (especially on first load when the model is cold). The service
configures generous timeouts via Polly:

- **Total request timeout:** 10 minutes
- **Per-attempt timeout:** 8 minutes
- **Retry:** 1 retry on network failure (exponential backoff with jitter)
- **Circuit breaker:** Effectively disabled (very lenient thresholds)

### Fallback

If LLM generation fails or is disabled (`EnableLlmGeneration: false`), the service falls back
to the static templates in `QuestionTemplates.cs` and `AnswerTemplates.cs`.

---

## Configuration

### SeederOptions

| Key | Type | Default | Description |
|---|---|---|---|
| `QuestionServiceUrl` | string | — | URL of the Question Service API |
| `ProfileServiceUrl` | string | — | URL of the Profile Service API |
| `VoteServiceUrl` | string | — | URL of the Vote Service API |
| `LlmApiUrl` | string | — | URL of the OpenAI-compatible LLM API |
| `LlmModel` | string | — | Model name to use (e.g., `phi3.5`, `ai/smollm2`) |
| `IntervalMinutes` | int | 10 | Minutes between seeding cycles |
| `MinAnswersPerQuestion` | int | 2 | Minimum answers to generate per question |
| `MaxAnswersPerQuestion` | int | 4 | Maximum answers to generate per question |
| `MaxSeederUsers` | int | 20 | Maximum number of seeder-prefixed users |
| `SeederUsernamePrefix` | string | `seeder-` | Username prefix for seeder-managed users |
| `EnableLlmGeneration` | bool | true | Use LLM for content generation (false = templates only) |
| `EnableVoting` | bool | true | Cast random votes after creating content |

### KeycloakOptions

| Key | Type | Description |
|---|---|---|
| `Url` | string | Keycloak base URL |
| `Realm` | string | Keycloak realm name |
| `AdminClientId` | string | Client ID for Admin API access (client_credentials grant) |
| `AdminClientSecret` | string | Client secret for Admin API access |
| `NextJsClientId` | string | Client ID for user token requests (password grant) |
| `NextJsClientSecret` | string | Client secret (if confidential client) |

### Environment-Specific Configurations

| Environment | LLM Model | Interval | Config File |
|---|---|---|---|
| **Local (default)** | `ai/smollm2` | 10 min | `appsettings.json` |
| **Development (Aspire)** | `ai/smollm2` | 1 min | `appsettings.Development.json` |
| **Staging (K8s)** | `phi3.5` | 60 min | `appsettings.Staging.json` |

---

## Project Structure

```
Overflow.DataSeederService/
├── Program.cs                  # Host setup, DI registration, HttpClient config
├── Models/
│   ├── SeederOptions.cs        # Configuration options
│   ├── ContentVariability.cs   # Variability enums and random profile generator
│   ├── Dtos.cs                 # DTOs for API communication
│   └── LlmModels.cs           # LLM request/response models
├── Services/
│   ├── SeederBackgroundService.cs  # Main orchestration loop
│   ├── UserGenerator.cs        # User pool management (create/rehydrate)
│   ├── QuestionGenerator.cs    # Question creation via API
│   ├── AnswerGenerator.cs      # Answer creation + accept via API
│   ├── VotingService.cs        # Random voting on questions/answers
│   ├── LlmClient.cs           # HTTP client for LLM API (no prompt logic)
│   ├── AuthenticationService.cs    # Keycloak user creation + token management
│   └── KeycloakAdminService.cs # Keycloak Admin API operations
└── Templates/
    ├── LlmPrompts.cs           # All LLM prompt templates with variability
    ├── QuestionTemplates.cs    # Fallback question titles + content
    └── AnswerTemplates.cs      # Fallback answer templates by style
```

---

## Local Development

### With .NET Aspire (recommended)

The Data Seeder Service is included in the Aspire AppHost and starts automatically:

```bash
cd Overflow.AppHost
dotnet run
```

The seeder will wait 30 seconds for other services to come up, then begin seeding.
Check the Aspire Dashboard at **http://localhost:18888** for logs and telemetry.

### LLM Setup

For LLM generation to work locally, you need a running LLM server:

**Option A — Ollama:**
```bash
ollama serve
ollama pull smollm2
# API available at http://localhost:11434/v1/chat/completions
```

**Option B — llama.cpp:**
```bash
# Start llama.cpp server on port 12434
# API available at http://localhost:12434/engines/llama.cpp/v1/chat/completions
```

**Option C — Disable LLM:**

Set `EnableLlmGeneration` to `false` in `appsettings.Development.json`. The seeder will
use the static fallback templates instead.

### Keycloak Requirements

The seeder requires:
- An `overflow-admin` client with **Service Accounts Enabled** and the `realm-admin` role
  (for creating users and resetting passwords via the Admin API).
- An `overflow-web` client (public or confidential) for obtaining user tokens via password grant.

See [Keycloak Setup](./KEYCLOAK_SETUP.md) for details.

---

## Kubernetes Deployment

In staging, the seeder runs as a single-replica Deployment in the `apps-staging` namespace.

### Key Differences from Local

| Aspect | Local | Staging (K8s) |
|---|---|---|
| LLM backend | llama.cpp / Ollama on host | Ollama pod in `apps-staging` |
| LLM model | `ai/smollm2` | `phi3.5` |
| Interval | 1 minute | 60 minutes |
| Service URLs | `localhost:PORT` | Kubernetes service names (`http://question-svc`) |
| Secrets | `appsettings.json` / user-secrets | Infisical SDK at runtime |

### Ollama in Kubernetes

The staging Ollama instance runs as a separate pod and is accessible at:
```
http://ollama.apps-staging.svc.cluster.local:11434/v1/chat/completions
```

---

## Troubleshooting

### Common Issues

**Seeder creates users but questions fail**
- Check that the Question Service is healthy: `kubectl logs -n apps-staging -l app=question-svc -f`
- Verify tags exist — the seeder needs at least one tag to create questions.

**LLM requests time out**
- First request after pod start may take several minutes (model cold-loading into memory).
- Check Ollama pod logs: `kubectl logs -n apps-staging -l app=ollama -f`
- If persistent, increase `TotalRequestTimeout` in `Program.cs`.

**"Not enough users" error**
- The Keycloak Admin API may be unreachable. Check connectivity.
- Ensure the `overflow-admin` client has the `realm-admin` role.
- Check: `kubectl logs -n apps-staging -l app=data-seeder-svc -f`

**Users created but tokens fail**
- The `overflow-web` client may not allow `password` grant type.
- Ensure Direct Access Grants are enabled in Keycloak for the client.

**Seeder users accumulating beyond 20**
- Old users without the `seeder-` prefix (created before the prefix was added) are orphaned.
  They're harmless but won't be reused. Clean them up manually in Keycloak if desired.

### Useful Commands

```bash
# Follow seeder logs
kubectl logs -n apps-staging -l app=data-seeder-svc -f

# Restart the seeder
kubectl rollout restart deployment/data-seeder-svc -n apps-staging

# Check seeder user count in Keycloak
# (via Keycloak Admin Console → Users → search "seeder-")

# Disable LLM temporarily (edit ConfigMap or env var)
kubectl set env deployment/data-seeder-svc -n apps-staging SeederOptions__EnableLlmGeneration=false
```

