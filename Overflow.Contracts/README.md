# Overflow.Contracts

Shared RabbitMQ message contracts — all `record` types used for inter-service communication via Wolverine.

---

## Events

| Contract                | Publisher                    | Subscribers                  |
|-------------------------|------------------------------|------------------------------|
| `QuestionCreated`       | QuestionService              | SearchService, StatsService  |
| `QuestionUpdated`       | QuestionService              | SearchService                |
| `QuestionDeleted`       | QuestionService              | SearchService                |
| `AnswerCountUpdated`    | QuestionService              | SearchService                |
| `AnswerAccepted`        | QuestionService              | SearchService                |
| `VoteCasted`            | VoteService                  | QuestionService              |
| `UserReputationChanged` | VoteService, QuestionService | ProfileService, StatsService |

---

## Helpers

### `ReputationHelper`

`ReputationHelper.MakeEvent()` creates a `UserReputationChanged` event with the correct reputation delta based on the
`ReputationReason`:

| Reason              | Delta |
|---------------------|-------|
| `QuestionUpvoted`   | +5    |
| `QuestionDownvoted` | −2    |
| `AnswerUpvoted`     | +5    |
| `AnswerDownvoted`   | −2    |
| `AnswerAccepted`    | +15   |

### `ReputationReason`

Enum defining all possible reputation change reasons.

### `VoteTargetType`

String constants for vote target types — use these instead of hardcoded `"Question"` / `"Answer"` strings:

```csharp
VoteTargetType.Question  // "Question"
VoteTargetType.Answer    // "Answer"
VoteTargetType.IsValid(targetType)  // validates input
```

---

## Possible Improvements

- **Add contract versioning strategy** — As the platform evolves, event schemas will need breaking changes. Introducing
  a versioning convention (e.g., `QuestionCreatedV2` or a `Version` property on contracts) with Wolverine's message
  forwarding would allow gradual migration without coordinated service deployments.
- **Add integration test helpers** — Provide a `ContractTestFixture` that makes it easy to publish test events and
  assert handler behavior. This would let each service's test suite verify it correctly handles every contract it
  subscribes to, catching deserialization issues early.

---

## Usage

Reference this project from any service that publishes or consumes events:

```xml
<ProjectReference Include="..\Overflow.Contracts\Overflow.Contracts.csproj"/>
```

**Event flow example:**

```
vote-svc ──► VoteCasted ──► question-svc (vote count)
         ──► UserReputationChanged ──► profile-svc, stats-svc
```

---

## Environments

| Environment | Branch | Namespace | URL |
|---|---|---|---|
| Local | — | (Aspire) | http://localhost:3000 |
| Staging | `development` | `apps-staging` | https://staging.devoverflow.org |
| Production | `main` | `apps-production` | https://devoverflow.org |

---

## Documentation

### Platform & Infrastructure

| Document | Description |
|---|---|
| [Quick Start](docs/QUICKSTART.md) | Local dev setup + full Kubernetes deployment guide |
| [Infrastructure](docs/INFRASTRUCTURE.md) | Architecture deep-dive, request flow, ingress routing, SSL, troubleshooting |
| [Network Architecture](docs/NETWORK_ARCHITECTURE.md) | Detailed network diagrams and connection flows |
| [Keycloak Setup](docs/KEYCLOAK_SETUP.md) | Realm/client config, audience mappers, Google SSO, local dev |
| [Google Auth Setup](docs/GOOGLE_AUTH_SETUP.md) | Google OAuth via Keycloak Identity Brokering |
| [Infisical Setup](docs/INFISICAL_SETUP.md) | All 33 secrets, how they flow from Infisical to services |
| [Kubernetes](k8s/README.md) | Kustomize structure, manifests, operations |
| [Terraform](terraform/README.md) | Project-specific Terraform (DBs, vhosts, ConfigMaps) |


---

## Key Design Decisions

- **One database per service** — each microservice owns its schema; no cross-service DB calls.
- **Event-driven** — services communicate via RabbitMQ. Wolverine handles outbox, retries, and routing.
- **Infisical at runtime** — no secrets baked into images. Every pod fetches secrets from Infisical on startup.
- **.NET Aspire for local dev** — one `dotnet run` starts the entire backend with all dependencies.
- **On-premises Kubernetes** — K3s runs on a home server. Cloudflare proxies requests and hides the origin IP.
