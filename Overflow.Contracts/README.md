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
| `QuestionUpvoted`   | +10   |
| `QuestionDownvoted` | -2    |
| `AnswerUpvoted`     | +10   |
| `AnswerDownvoted`   | -2    |
| `AnswerAccepted`    | +15   |

### `ReputationReason`

Enum defining all possible reputation change reasons.

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

Wolverine auto-discovers handlers by convention — any class with a `Handle` or `HandleAsync` method matching a contract
type is registered automatically.
