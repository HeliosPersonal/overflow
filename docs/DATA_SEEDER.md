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
5. [Content Generation](#content-generation)
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
| **LLM Model** | `qwen2.5:7b` (all environments), configurable per environment |
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
- **LLM API** — runs the multi-step generation pipeline (topic seed, question, answer, critic, repair)

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
Step 3  │  Generate Question  [LLM Pipeline: Steps 1–2]
        │  → Fetch available tags from question-svc
        │  → Select 1-3 random tags
        │  → LLM Step 1: Generate a TopicSeed (structured problem description)
        │  → LLM Step 2: Generate a StructuredQuestion from the seed (title + body + tags + code)
        │  → Fallback to static templates if pipeline fails
        │  → POST to question-svc
        │
Step 4  │  Realistic Delay (5-15 seconds)
        │
Step 5  │  Generate Answers  [LLM Pipeline: Steps 3–5]
        │  → Pick 2-5 random answerers (excluding the asker)
        │  → LLM Step 3: Generate a StructuredAnswer (body + code snippet)
        │  → LLM Step 4: Critic evaluation (optional, EnableCriticPass)
        │  → LLM Step 5: Repair pass if critic flagged issues (optional, EnableRepairPass)
        │  → Fallback to static templates if pipeline fails
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

The seeder maintains a **fixed pool of 20 users** identified by a `seeder-` email prefix.
This ensures the seeder doesn't pollute the system with hundreds of users while still having
enough diversity for realistic interactions.

### How It Works

1. **Discovery** — On each cycle, the seeder searches Keycloak for existing users whose email
   starts with `seeder-` (via the Admin API search endpoint).

2. **Rehydration** — For each found user, the service resets their password (via Keycloak Admin API)
   and obtains a fresh JWT token. This makes the service restart-safe — no in-memory state is
   required to persist between restarts.

3. **Creation** — If fewer than `MaxSeederUsers` (default: 20) users exist, new ones are created to
   fill the gap. Each new user is registered in Keycloak and their profile is auto-created by hitting
   the Profile Service (which triggers the `UserProfileCreationMiddleware`).

4. **Isolation** — Only `seeder-*` prefixed users are touched. Real users and users created through
   the frontend are never affected.

### Email Format

> **Note:** `registrationEmailAsUsername` is enabled in Keycloak — email is the unique
> user identifier (username). The seeder generates emails with a recognizable prefix.

```
seeder-{namePart}{randomSuffix}@overflow.local
```

Example: `seeder-johndoe4521@overflow.local`, `seeder-janesmith8734@overflow.local`

---

## Content Generation

The service uses a **multi-step LLM pipeline** designed to produce high-quality, realistic
StackOverflow-style content even with mid-size models (7B parameters).

All LLM calls return **strict JSON** — the `LlmClient` deserializes typed DTOs, retries on
parse failure, and converts final Markdown content to HTML for storage.

### Generation Pipeline Overview

```
Tag
 │
 ▼
[Step 1] TopicSeed          (temp=0.7)
  → Structured problem description
  → topic, difficulty, problem_type, bug_reason, key_entities, solution_hint
 │
 ▼
[Step 2] StructuredQuestion  (temp=0.5)
  → Realistic StackOverflow question
  → title, body_markdown, tags, code_snippet
 │                                   │
 ▼                                   ▼ (for each answerer)
POST to question-svc          [Step 3] StructuredAnswer  (temp=0.4)
                                → Direct solution to the question
                                → body_markdown, code_snippet, accepted
                               │
                               ▼  (if EnableCriticPass=true)
                         [Step 4] Critic          (temp=0.2)
                               → Evaluates title relevance, body clarity,
                                 answer correctness, code validity,
                                 UI contamination check
                               → { valid, issues[] }
                               │
                               ▼  (if issues found AND EnableRepairPass=true)
                         [Step 5] Repair          (temp=0.3)
                               → Fixes flagged issues
                               → { question, answer } (corrected)
                               │
                               ▼
                         POST to question-svc
```

### JSON Contracts

All steps communicate via strict JSON. The model is instructed to return **only** a JSON object
with no markdown fences, no explanatory text, no StackOverflow UI elements (vote counts,
"N answers", usernames, "Viewed N times", comment threads, etc.).

**TopicSeed** (Step 1):
```json
{
  "topic": "python tkinter",
  "difficulty": "beginner",
  "problem_type": "ui refresh",
  "bug_reason": "label text not updating because StringVar is not used",
  "key_entities": ["Button", "Label", "StringVar"],
  "solution_hint": "use StringVar and configure() to update the label text"
}
```

**QuestionGenerationDto** (Step 2):
```json
{
  "title": "Tkinter label text not updating after button click",
  "body_markdown": "I have a simple Tkinter app...\n\n```python\n...\n```\n\n**Expected:** ...\n**Actual:** ...",
  "tags": ["python", "tkinter", "gui"],
  "code_snippet": "label.text = 'Updated'  # incorrect"
}
```

**AnswerGenerationDto** (Step 3):
```json
{
  "body_markdown": "The problem is that `label.text` is not a valid Tkinter attribute...",
  "code_snippet": "label.config(text='Updated')  # correct",
  "accepted": false
}
```

**CriticResultDto** (Step 4):
```json
{
  "valid": true,
  "issues": []
}
```

**RepairResultDto** (Step 5):
```json
{
  "question": { "title": "...", "body_markdown": "...", "tags": [...], "code_snippet": "..." },
  "answer":   { "body_markdown": "...", "code_snippet": "...", "accepted": false }
}
```

### Per-Step Temperatures

| Step | Purpose | Temperature |
|---|---|---|
| TopicSeed | Creative problem diversity | 0.7 |
| StructuredQuestion | Balanced structure and creativity | 0.5 |
| StructuredAnswer | Accurate, focused solution | 0.4 |
| Critic | Deterministic evaluation | 0.2 |
| Repair | Targeted corrections | 0.3 |

### Retry on JSON Parse Failure

If the model returns malformed JSON, `LlmClient` retries the call up to `MaxGenerationRetries`
times (default: 2) before giving up and falling back to static templates. The retry logic:

1. Strips markdown fences (` ```json ... ``` `)
2. Extracts the first `{...}` or `[...]` block
3. Attempts `JsonSerializer.Deserialize<T>()`
4. Retries on `JsonException`

### Fallback Templates

When LLM generation fails or is disabled:

- **Questions** — 12 paired (title, content) templates covering common programming topics
- **Answers** — 12 varied answer templates (concise fixes, step-by-step guides, code examples)

### Answer Style Variability

Answers are still generated with random style variation passed to the StructuredAnswer prompt:

| Style | Behaviour |
|---|---|
| Neutral | Clear, helpful, professional |
| Conversational | Friendly and approachable |
| Formal | Professional, precise, concise |
| StepByStep | Numbered steps, one concept each |
| CodeHeavy | Lead with code, minimal prose |

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

### Model Choice

All environments now use **`qwen2.5:7b`** via Ollama. The upgrade from `qwen2.5:3b` improves:

- JSON compliance (fewer malformed responses, fewer retries)
- Title realism (proper StackOverflow question phrasing)
- Code snippet quality (more plausible, runnable examples)
- Answer correctness (fewer hallucinations)
- Critic accuracy (better issue detection)

The model is instructed to write body content in **Markdown**; `LlmClient.SanitizeHtml` converts
it to HTML for storage and display.

### Timeout & Resilience

LLM requests can take minutes (especially on first load when the model is cold). The service
configures generous timeouts via Polly:

- **Total request timeout:** 10 minutes
- **Per-attempt timeout:** 8 minutes
- **Retry:** 1 retry on network failure (exponential backoff with jitter)
- **Circuit breaker:** Effectively disabled (very lenient thresholds)

The multi-step pipeline is designed to be resilient: each step can fail independently.
If any step returns null or unparseable JSON, the service falls back to static templates
rather than crashing or producing empty content.

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
| `LlmModel` | string | — | Model name (e.g., `qwen2.5:7b`) |
| `IntervalMinutes` | int | 10 | Minutes between seeding cycles |
| `MinAnswersPerQuestion` | int | 2 | Minimum answers to generate per question |
| `MaxAnswersPerQuestion` | int | 4 | Maximum answers to generate per question |
| `MaxSeederUsers` | int | 20 | Maximum number of seeder-prefixed users |
| `SeederUsernamePrefix` | string | `seeder-` | Username prefix for seeder-managed users |
| `EnableLlmGeneration` | bool | true | Use LLM for content generation (false = templates only) |
| `EnableVoting` | bool | true | Cast random votes after creating content |
| `MaxGenerationRetries` | int | 2 | JSON parse retry attempts per pipeline step |
| `EnableCriticPass` | bool | true | Run critic evaluation after answer generation |
| `EnableRepairPass` | bool | true | Run repair pass when critic flags issues |

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
| **Local (default)** | `qwen2.5:7b` | 10 min | `appsettings.json` |
| **Development (Aspire)** | `qwen2.5:7b` | 1 min | `appsettings.Development.json` |
| **Staging (K8s)** | `qwen2.5:7b` | 60 min | `appsettings.Staging.json` |

### Disabling Pipeline Steps

To speed up generation (at the cost of quality) or debug specific steps:

```bash
# Disable critic + repair (fastest, single LLM call per answer)
kubectl set env deployment/data-seeder-svc -n apps-staging \
  SeederOptions__EnableCriticPass=false \
  SeederOptions__EnableRepairPass=false

# Disable LLM entirely (use static templates only)
kubectl set env deployment/data-seeder-svc -n apps-staging \
  SeederOptions__EnableLlmGeneration=false

# Reduce retry attempts (faster failure, more fallbacks to templates)
kubectl set env deployment/data-seeder-svc -n apps-staging \
  SeederOptions__MaxGenerationRetries=0
```

---

## Project Structure

```
Overflow.DataSeederService/
├── Program.cs                  # Host setup, DI registration, HttpClient config
├── Models/
│   ├── SeederOptions.cs        # Configuration options (incl. new pipeline flags)
│   ├── ContentVariability.cs   # Length + AnswerStyle enums, random profile generator
│   ├── Dtos.cs                 # DTOs for API communication (questions, answers, profiles)
│   ├── LlmModels.cs            # LLM HTTP request/response models
│   └── LlmGenerationDtos.cs    # Typed pipeline DTOs (TopicSeedDto, QuestionGenerationDto,
│                               #   AnswerGenerationDto, CriticResultDto, RepairResultDto)
├── Services/
│   ├── SeederBackgroundService.cs  # Main orchestration loop (8-step cycle)
│   ├── UserGenerator.cs        # User pool management (create/rehydrate)
│   ├── QuestionGenerator.cs    # Question pipeline: TopicSeed → StructuredQuestion
│   ├── AnswerGenerator.cs      # Answer pipeline: StructuredAnswer → Critic → Repair
│   ├── VotingService.cs        # Random voting on questions/answers
│   ├── LlmClient.cs            # HTTP client + GenerateStructuredAsync<T> + pipeline methods
│   ├── AuthenticationService.cs    # Keycloak user creation + token management
│   └── KeycloakAdminService.cs # Keycloak Admin API operations
└── Templates/
    ├── LlmPrompts.cs           # All LLM prompt templates (5-step pipeline)
    ├── QuestionTemplates.cs    # 12 paired (title, content) fallback templates
    └── AnswerTemplates.cs      # 12 fallback answer templates
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

For LLM generation to work locally, you need a running LLM server with `qwen2.5:7b`:

**Option A — Ollama (recommended):**
```bash
ollama serve
ollama pull qwen2.5:7b
# API available at http://localhost:11434/v1/chat/completions
# Update LlmApiUrl in appsettings.Development.json accordingly
```

**Option B — llama.cpp:**
```bash
# Start llama.cpp server on port 12434 with qwen2.5-7b-instruct.gguf
# API available at http://localhost:12434/engines/llama.cpp/v1/chat/completions
```

**Option C — Disable LLM:**

Set `EnableLlmGeneration` to `false` in `appsettings.Development.json`. The seeder will
use the static fallback templates instead.

**Option D — Disable critic/repair only:**

Keep LLM on but skip validation steps for faster local iteration:
```json
"EnableCriticPass": false,
"EnableRepairPass": false
```

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
| LLM model | `qwen2.5:7b` | `qwen2.5:7b` |
| Interval | 1 minute | 60 minutes |
| Service URLs | `localhost:PORT` | Kubernetes service names (`http://question-svc`) |
| Secrets | `appsettings.json` / user-secrets | Infisical SDK at runtime |

### Ollama in Kubernetes

The staging Ollama instance runs as a separate pod and is accessible at:
```
http://ollama.apps-staging.svc.cluster.local:11434/v1/chat/completions
```

Make sure `qwen2.5:7b` is pulled into the Ollama pod before the seeder runs:
```bash
kubectl exec -n apps-staging deploy/ollama -- ollama pull qwen2.5:7b
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

**Generation pipeline always falls back to templates**
- Check logs for `[TopicSeed]`, `[StructuredQuestion]`, `[StructuredAnswer]` messages.
- JSON parse failures are logged with the raw response — look for `JSON parse failed` warnings.
- Try `MaxGenerationRetries: 3` to give the model more attempts.
- Verify the model is `qwen2.5:7b` — smaller models produce less reliable JSON.

**Critic always marks answers as invalid**
- This causes the repair pass to run on every answer, slowing generation significantly.
- Check `[Critic]` log lines to see what issues are being flagged.
- Temporarily set `EnableRepairPass: false` to skip repair while debugging prompts.
- Or set `EnableCriticPass: false` to bypass both steps.

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

### Reading Pipeline Logs

The seeder logs structured messages for each pipeline step. Key log prefixes:

| Log Prefix | Meaning |
|---|---|
| `[TopicSeed]` | Step 1 — topic seed generation |
| `[StructuredQuestion]` | Step 2 — question generation from seed |
| `[StructuredAnswer]` | Step 3 — answer generation |
| `[Critic]` | Step 4 — critic evaluation result |
| `[Repair]` | Step 5 — repair pass result |
| `[Pipeline]` | QuestionGenerator pipeline orchestration |
| `[AnswerPipeline]` | AnswerGenerator pipeline orchestration |

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

# Disable critic + repair (faster generation, skip validation)
kubectl set env deployment/data-seeder-svc -n apps-staging \
  SeederOptions__EnableCriticPass=false \
  SeederOptions__EnableRepairPass=false

# Pull qwen2.5:7b into Ollama pod
kubectl exec -n apps-staging deploy/ollama -- ollama pull qwen2.5:7b
```
