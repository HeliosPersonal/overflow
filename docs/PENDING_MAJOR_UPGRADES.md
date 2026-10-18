# Pending major upgrades

Backlog of dependency upgrades that were **deliberately not applied** during the
2026-10-18 maintenance pass. That pass took every non-breaking minor/patch across
`Directory.Packages.props`, `webapp/package.json` and the `infrastructure-helios`
Terraform providers; everything listed here needs a major-version migration and
therefore its own task.

Re-check this list before starting any of them — versions move.

---

## 1. WolverineFx 5 → 6 (**security-driven, highest priority**)

| Package | Current | Target |
|---|---|---|
| `WolverineFx.EntityFrameworkCore` | 5.21.0 | 6.51.0 |
| `WolverineFx.Marten` | 5.21.0 | 6.51.0 |
| `WolverineFx.Postgresql` | 5.21.0 | 6.51.0 |
| `WolverineFx.RabbitMQ` | 5.21.0 | 6.51.0 |
| `WolverineFx` (root, currently unreferenced) | 5.21.0 | 6.51.0 |
| `Marten` (transitive, pinned in CPM) | 8.38.2 | 9.50.3 |

**Why it matters.** `Marten` 8.38.2 still carries
[CVE-2026-75513](https://github.com/advisories/GHSA-rfx3-98h7-v3xp) — SQL injection in
the LINQ provider, **critical, CVSS 9.1**. It is the one vulnerability `dotnet build`
still reports (`NU1904` on `Overflow.StatsService`).

It is **not fixable inside Marten 8**: the advisory covers 7.0.0–9.12.0 with no 8.x
backport, so the fix only exists in Marten ≥ 9.13.0 — and `WolverineFx.Marten` 5.21.0
floors Marten at 8.25.0 while 6.51.0 pulls 9.50.3. The Wolverine major is the only
route to a patched Marten.

**Current exposure is low but not zero.** The advisory's attack vectors are
`Dictionary<,>` indexer keys in `Where` filters; `Overflow.StatsService` uses neither
those nor any other known-vulnerable path today. The risk is that a future query
introduces one silently. Treat this as "fix on the next planned cycle", not "hotfix
tonight" — but do not let it sit indefinitely.

The sibling advisory
[CVE-2026-45288](https://github.com/advisories/GHSA-vmw2-qwm8-x84c) (regConfig
full-text-search injection, CVSS 9.8) **is already closed** by the 8.38.2 pin.

**Migration notes.** Wolverine 6 changes handler discovery and envelope APIs; the
durable-outbox registration in `Overflow.QuestionService` and the
`UseConventionalRouting()` setup in `Overflow.ServiceDefaults` are the places to
re-verify. Marten 9 changes projection registration — `TopUsersProjection` and
`TrendingTagsProjection` will need review. Budget time for a real test pass against
Postgres, not just a compile.

---

## 2. .NET — other majors

| Package | Current | Latest | Notes |
|---|---|---|---|
| `Refit` / `Refit.HttpClientFactory` | 10.2.0 | 16.4.0 | Six majors of drift. Interface-definition and serializer changes; `Overflow.DataSeederService` is the main consumer. |
| `StackExchange.Redis` | 2.13.17 | 3.4.5 | Used by `Overflow.EstimationService` and FusionCache's Redis backplane. Verify FusionCache's backplane package supports v3 **before** bumping — a mismatch here breaks planning-poker state. |

### Housekeeping, not a major

`Aspire.Hosting.NodeJs` is stuck at **9.5.2** while every other Aspire package is on
13.6.1 — the package line simply stopped at 9.x and was folded into Aspire 13 core.
The build resolves fine today. When touching `Overflow.AppHost`, check whether the
`webapp` Node resource should move to the built-in Aspire 13 API and the package be
dropped entirely.

---

## 3. webapp — majors

| Package | Current | Latest | Notes |
|---|---|---|---|
| `@heroui/react` | 2.8.10 | 3.2.6 | **Largest item here.** v3 is a ground-up rewrite, not a rename pass. Touches nearly every component in `src/components` and `src/app`. Deserves its own branch and a visual review against `webapp/STYLE.md`. |
| `typescript` | 5.9.3 | 7.0.2 | The native-port compiler. Expect stricter inference; `src/lib/logger.ts` (6 × `no-explicit-any`) is the likely trouble spot. |
| `@infisical/sdk` | 4.0.6 | 5.0.2 | **Security-relevant.** The only remaining *critical* `npm audit` finding is `fast-xml-parser` reached through this SDK. Server-side secrets loading only — not in the browser bundle — so exposure is limited, but this is the cheapest critical to retire. |
| `framer-motion` | 12.43.0 | 14.3.0 | Two majors. Animation APIs used across planning-poker components. |
| `lucide-react` | 0.577.0 | 1.59.0 | First stable major; mostly an icon-name audit. |
| `eslint` | 9.39.5 | 10.13.0 | Flat-config changes; coordinate with `eslint-config-next`. 9.39.5 is already marked end-of-support by upstream. |
| `@types/node` | 20.19.43 | 26.6.5 | Should follow whatever Node version CI and the Docker image actually run. |
| `@dicebear/core` + `@dicebear/collection` | 9.4.3 | 10.7.0 | Avatar generation; bump both together or styles break. |
| `next-auth` | 5.0.0-beta.32 | still beta | No stable v5 yet. Stay on the beta line and keep bumping betas. |

### Next.js 16.4 deprecation — do this soon, it is cheap

`next build` now warns that the **`middleware` file convention is deprecated in favour
of `proxy`**. `webapp/src/middleware.ts` (auth redirects to `/auth-gate`) still works
and the build output already labels the route `Proxy (Middleware)`. There is an
automated codemod:

```bash
cd webapp && npx @next/codemod@canary middleware-to-proxy .
```

`CLAUDE.md` references `middleware.ts` by name — update it in the same change.

### Three lint errors introduced by new rules

Bumping `eslint-config-next` 16.2.1 → 16.4.2 shipped new rules that flag
**pre-existing** code. These are not regressions from the dependency bump, and lint
was already failing before it (12 errors at baseline, 14 now):

| File | Rule |
|---|---|
| `src/app/(main)/planning-poker/[roomId]/RoomClient.tsx:190` | `react-hooks/set-state-in-effect` |
| `src/components/SideMenu.tsx:21` | `react-hooks/set-state-in-effect` |
| `src/app/(main)/planning-poker/new/CreateRoomForm.tsx:69` | `@next/next/no-location-assign-relative-destination` |

`npm run lint` is not wired into `.github/workflows/ci-cd.yml`. Worth a separate
clean-up task — ideally alongside making lint a CI gate so the count stops drifting.

---

## 4. infrastructure-helios

Audited against the live `helios` cluster on 2026-10-18 (k3s v1.34.6, single node
`k3s-01`, 8 vCPU / 45.9 GiB, amd64, **no GPU resources advertised**).

`terraform validate` passes. **`terraform plan` was still not run** — the state backend
is Azure Blob and the local Azure token is expired (`AADSTS70043`); it needs a fresh
`az login`. So the items below are verified against the *cluster*, not against a plan
diff.

### Terraform providers

| Provider | Locked | Constraint | Latest | Notes |
|---|---|---|---|---|
| `hashicorp/kubernetes` | 2.38.0 | `~> 2.38` | 3.3.0 | Major. Requires raising the constraint in `provider.tf`. |
| `hashicorp/helm` | 2.17.0 | `~> 2.17` | 3.3.0 | Major. v3 reworks `helm_release`; the `set {}` blocks in `ollama.tf` change shape. |

`null` (3.3.2) and `local` (2.9.1) are already current — they were upgraded in-place
since their constraints allowed it.

### Helm charts — deployed versions match the Terraform pins

Read off the `helm.sh/chart` labels in-cluster. **No drift between code and cluster** —
every pinned chart is the one actually running, so a bump is a deliberate choice rather
than a reconciliation.

| Chart | Pinned = deployed | App version | Notes |
|---|---|---|---|
| `ollama` (otwld) | 1.54.0 | 0.19.0 (image overridden) | Latest chart is **1.89.0**. The values template in `ollama.tf` targets the 1.54.0 schema (`persistentVolume.existingClaim`, `ollama.models.pull`, `extraEnv`) — re-check that schema before bumping. |
| `ingress-nginx` | 4.15.1 | 1.15.1 | |
| `postgresql` | 18.5.14 | 18.3.0 | Stateful — needs a backup/restore plan. |
| `redis` | 0.26.8 | 8.6.2 | Stateful. |
| `rabbitmq` | 0.19.9 | 4.2.5 | Stateful. |
| `keycloak` | 0.19.8 | 26.5.6 | Stateful (own `postgres-0.9.0` / 18.0 StatefulSet). |
| `kube-state-metrics` | 7.2.2 | 2.18.0 | |
| `alloy` | 1.6.2 | v1.14.0 | |
| `headlamp` | 0.39.0 | 0.39.0 | |

### Pre-existing drift noticed in passing

- `terraform fmt` flags **`ingress.tf`** (misaligned `locals` block). Untouched to keep
  this pass reviewable — one `terraform fmt ingress.tf` fixes it.
- `terraform validate` cannot run without `terraform/certs/origin.{crt,key}`
  (gitignored Cloudflare origin cert). Validation above was done against throwaway
  stub files, which were then deleted.

---

## Follow-up: right-size the Ollama pod (recommended, not optional)

Cluster measurements from 2026-10-18 make this more pressing than it looked on paper.

The node has **45.9 GiB** of RAM. `ollama_memory_limit` is **40Gi** — a single pod
allowed to claim **87% of the whole node**. It was raised to that when the DataSeeder
was pointed at a 26B model. Node-wide allocation is already:

```
cpu      3560m (44%) requests    12050m (150%) limits
memory  10552Mi (22%) requests   61738Mi (131%) limits
```

Ollama's 40Gi is the single largest contributor to that 131% memory overcommit. It is
a ceiling rather than a reservation (`ollama_memory_request` stays 4Gi), so nothing is
wasted while idle — but if Ollama ever did climb toward it, the kernel would start
reaping co-tenants, and *every* Overflow service shares this one node.

With `qwen3.5:4b` (~3.3 GB on disk, well under that resident) something in the
**8–12Gi** range is a far more honest ceiling and still leaves room to experiment with
a 9B model. Not changed in this pass because it is a deployment-affecting value and
no `terraform plan` could be run to preview it.

### Stale models on the PVC — ~22 GB reclaimable

The `ollama` PVC is provisioned at **20Gi**, not the 40Gi that `ollama_storage_size`
suggests: `ollama.tf` carries `lifecycle { ignore_changes = [...requests] }` because
`local-path` cannot expand volumes, so the variable never reached the existing claim.
(`local-path` is a host directory and does not enforce the quota, which is why the
contents already exceed 20Gi.)

Currently on the volume:

| Model | Size | Last modified |
|---|---|---|
| `gemma4:26b` | 17 GB | ~6 months ago |
| `qwen2.5:7b` | 4.7 GB | ~7 weeks ago |

Both become dead weight once `qwen3.5:4b` is pulled. Removing them frees ~22 GB:

```bash
kubectl -n infra-production exec deploy/ollama -- ollama rm gemma4:26b qwen2.5:7b
```

Do this **after** confirming the new model serves correctly, not before.
