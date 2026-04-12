# Overflow Project & Infrastructure Showcase

> **Target Audience:** Technical decision-makers, senior engineers, architects.
> **Focus:** Real-world enterprise engineering practices utilizing schemas instead of massive text blocks to visualize the system.

---

## Slide 1: High-Level Cloud Infrastructure
*Self-hosted K3s cluster behind Cloudflare, routing to discrete microservices.*

```mermaid
flowchart TD
    Browser[Browser] -->|HTTPS| CF[Cloudflare Edge \n CDN, WAF, SSL]
    CF -->|HTTPS| Router[Home Router]
    Router -->|Port Forward 443| Ingress[NGINX Ingress - K3s Node]
    
    subgraph K3s Cluster
        Ingress -->|"/"| WebApp[Next.js App]
        Ingress -->|"/api/questions/*"| Services[Backend Services]
        
        subgraph infra-production [Shared Infrastructure Layer]
            DB[(PostgreSQL)]
            RMQ[[RabbitMQ]]
            KC{Keycloak}
            TS[(Typesense)]
            RD[(Redis)]
        end
        Services -.-> infra-production
    end
```

---

## Slide 2: Overflow Microservices General Schema
*A Database-per-Service architecture communicating via RabbitMQ and Wolverine.*

```mermaid
flowchart LR
    Client[Client Apps]
    NGINX[NGINX PROXY]
    
    subgraph AppHost [App Host]
        direction LR
        WebApp[WebApp\nNextJS]
        BFF[BFF\nNextJS]
        Gateway[GATEWAY]
        
        subgraph KC_Group [ ]
            KC[Keycloak] --> KCDB[(Keycloak\nDatabase)]
        end
        subgraph QS_Group [ ]
            QS[Question-svc] --> QSDB[(Postgres\nDatabase)]
        end
        subgraph PS_Group [ ]
            PS[Profile-svc] --> PSDB[(Postgres\nDatabase)]
        end
        subgraph SS_Group [ ]
            SS[Search-svc] --> SSDB[(Typesense\nDatabase)]
        end
        subgraph StS_Group [ ]
            StS[Stat-svc] --> StSDB[(Postgres\nDatabase)]
        end
        subgraph VS_Group [ ]
            VS[Vote-svc] --> VSDB[(Postgres\nDatabase)]
        end
        
        EventBus[[Event Bus\nPublish/Subscribe]]
    end

    %% Left-to-right ingress flow
    Client --> NGINX
    NGINX --> WebApp
    WebApp --> BFF
    BFF ---> Gateway
    
    %% Gateway to services routing
    Gateway ---> KC_Group
    Gateway ---> QS_Group
    Gateway ---> PS_Group
    Gateway ---> SS_Group
    Gateway ---> StS_Group
    Gateway ---> VS_Group

    %% Event Bus connects directly to the Subgraph dashed borders to match the image accurately
    QS_Group <-->|✉️| EventBus
    PS_Group <-->|✉️| EventBus
    SS_Group <-->|✉️| EventBus
    StS_Group <-->|✉️| EventBus
    VS_Group <-->|✉️| EventBus

    %% Styling mimicking the original dashed boxes
    style KC_Group fill:none,stroke:#FFD700,stroke-width:2px,stroke-dasharray: 5 5
    style QS_Group fill:none,stroke:#FFD700,stroke-width:2px,stroke-dasharray: 5 5
    style PS_Group fill:none,stroke:#555,stroke-width:2px,stroke-dasharray: 5 5
    style SS_Group fill:none,stroke:#555,stroke-width:2px,stroke-dasharray: 5 5
    style StS_Group fill:none,stroke:#555,stroke-width:2px,stroke-dasharray: 5 5
    style VS_Group fill:none,stroke:#555,stroke-width:2px,stroke-dasharray: 5 5
```
*Legend: Solid lines = Synchronous API / Database calls. Dashed lines = Asynchronous RabbitMQ Events.*

---

## Slide 3: Event-Driven Flow & The Durable Outbox
*How the system safely posts a question and avoids data-loss on crashes.*

```mermaid
sequenceDiagram
    participant U as Next.js WebApp
    participant QS as QuestionService
    participant DB as PostgreSQL (questions DB)
    participant RMQ as RabbitMQ Transport
    participant SS as SearchService
    participant StS as StatsService

    U->>QS: POST /api/questions { title, body }
    activate QS
    QS->>DB: BEGIN Transaction
    QS->>DB: INSERT Question Entity (EF Core)
    QS->>DB: INSERT QuestionCreated (Wolverine Outbox)
    QS->>DB: COMMIT Transaction
    QS-->>U: 201 Created
    deactivate QS

    Note over QS, DB: Guaranteed Atomic write. No isolated message failures.
    
    QS->>RMQ: Outbox Agent Relays 'QuestionCreated'
    par To Search
        RMQ-->>SS: Handle QuestionCreated
        SS->>Search Engine: Index Document in Typesense
    and To Stats
        RMQ-->>StS: Handle QuestionCreated
        StS->>Stats DB: Append to Marten Event Stream
    end
```

---

## Slide 4: Real-Time Planning Poker (EstimationService)
*No RabbitMQ used here—state managed via PostgreSQL and cross-pod Redis instances.*

```mermaid
flowchart LR
    Client1[Browser 1]
    Client2[Browser 2]
    
    subgraph EstimationService [Kubernetes Replicas]
        Pod1[Estimation Pod A]
        Pod2[Estimation Pod B]
    end
    
    DBE[(DB: estimations)]
    RD[(Redis PubSub)]
    
    Client1 <-->|WebSocket\nRead-Only Push| Pod1
    Client1 -->|HTTP POST\nVote| Pod1
    
    Client2 <-->|WebSocket\nRead-Only Push| Pod2
    
    Pod1 -->|1. Mutate State| DBE
    Pod1 -.->|2. Publish RoomUpdated| RD
    RD -.->|3. Notify Pods| Pod2
    Pod2 -.->|4. Push Snapshot| Client2
```

---

## Slide 5: The "Guest Auth" User Lifecycle Flow
*To eliminate dual auth paths (JWT vs cookies), guests are assigned real Keycloak accounts dynamically.*

```mermaid
sequenceDiagram
    participant User
    participant WebApp as Next.js
    participant KCApi as Next.js Route (/api/auth/anonymous)
    participant KC as Keycloak (Admin API)
    
    User->>WebApp: Clicks "Continue as Guest"\n(Provides 'Display Name')
    WebApp->>KCApi: POST /api/auth/anonymous
    activate KCApi
    KCApi->>KC: Create User in Keycloak\n(anon_XXX@... + random PW)
    KC-->>KCApi: User Created successfully
    KCApi-->>WebApp: Returns auto-generated Credentials
    deactivate KCApi
    
    WebApp->>KC: NextAuth SignIn (Credentials Provider)
    KC-->>WebApp: Issues JWT
    WebApp-->>User: Provides Authenticated Guest Session
    Note over User, KC: User is now a real user system-wide.
```

---

## Slide 6: CI/CD & Terraform Deployment Automations
*A robust path from Git push to Kubernetes orchestration.*

```mermaid
flowchart LR
    Dev[Developer] -->|Push PR to main| GH[GitHub Actions]
    
    subgraph CI/CD Pipeline
        GH -->|1. Build & Test| DotNet[.NET Build]
        DotNet -->|2. Docker Build| GHCR[GHCR Docker Registry]
        GHCR -->|3. IaC Automation| TF[Terraform: DBs, VHosts,\nConfigMaps]
        TF -.->|Reads state| AzureState[(Azure Blob State)]
        TF -->|4. Deploy Manifests| K8s[K3s Cluster Kustomize]
    end
    
    subgraph Secrets Pipeline
        Inf[Infisical Secret Vault] -.->|Injected on-the-fly| GH
        Inf -.->|Runtime SDK| K8s
    end
```
