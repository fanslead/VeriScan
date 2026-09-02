# VeriScan

[中文](README.md) | [English](README.en.md)

VeriScan is a content-safety moderation service for business applications. It uses versioned keyword rules for low-cost screening, routes unresolved content to a configurable external AI provider, and exposes a batch API secured by application-specific API keys. The system records and returns `pass`, `reject`, or `review`; a `review` decision is handed back to the caller, and VeriScan does not operate a second human-review workflow.

> This repository is still in local development and validation. The benchmark below is a short single-machine run, not a production SLA. A pre-authentication ingress gate now turns overload into controlled HTTP `429` responses, but real-provider, soak, and production-capacity tests still belong in the target environment.

## Screenshots

### Risk overview

![VeriScan risk overview](docs/images/readme/dashboard.jpg)

### Operations-friendly rule editor

![VeriScan rule editor](docs/images/readme/rule-editor-v2.jpg)

Rules are expressed in business language and may match keywords, common formats such as phone numbers, email addresses, and links, or combinations of words that occur together. Operators do not need to learn internal rule types, regular expressions, category codes, or decimal weights; technical safety limits remain behind an optional advanced section.

### External AI configuration

![VeriScan AI configuration](docs/images/readme/ai-configuration.jpg)

The admin console configures the model, request URL, protocol, timeout, concurrency, and provider API key. The current adapters support Chat Completions, the Responses API, and Anthropic Messages request formats. A saved secret is never returned in plaintext.

## Highlights

- **Two-stage moderation:** compiled keyword rules run first; trusted hard rejects return immediately, while unresolved content is sent to external AI.
- **Governed policies:** validate drafts, publish immutable revisions, copy a published revision, bind an application explicitly, and preserve the effective policy revision on every request.
- **External AI routing:** manage model endpoints in the console, test connectivity, publish, and activate a configuration. Missing or failed AI calls conservatively return `review` according to policy instead of pretending an AI decision exists.
- **Per-application authentication:** each application owns independent API keys with scopes, expiry, rotation, and revocation. Usage and decision statistics are attributed to that application.
- **Reliable result webhooks:** each application can configure, test, enable, or disable a webhook. Async terminal events enter a durable local queue before Svix signs, delivers, and retries them.
- **Audit records, not a review queue:** requests, items, effective policies, routes, risk scores, and final states are recorded. Human review remains the caller's responsibility.
- **Small operational footprint:** ASP.NET Core 10, PostgreSQL 16, Redis, and a React 19 + Vite admin console managed with pnpm.

## Repository layout

```text
apps/
  api/                  ASP.NET Core 10 API
  admin/                React 19 + Semi Design admin console
packages/
  contracts/            Planned OpenAPI-derived client and shared contracts
tests/
  backend/              Backend unit and integration tests
  performance/          Rule-engine baseline and HTTP load harness
infra/                  Local PostgreSQL, Redis, Keycloak, and Svix dependencies
docs/                   Implementation, acceptance, and UI documentation
```

## Local setup

Prerequisites: .NET SDK 10.0.400, Node.js 24+, pnpm 11+, and Docker.

### 1. Start the complete system with Docker

```bash
pnpm install
cp infra/.env.example infra/.env
# Change the example passwords and generate the Svix token described in infra/README.md
docker compose --env-file infra/.env -f infra/compose.yaml up -d --build --wait
```

Default endpoints:

- Admin console: `http://localhost:5173`
- Moderation API: `http://localhost:5000`
- Keycloak: `http://localhost:8080`

The local seed account is `veriscan-admin` with password `veriscan-local-admin-change-me`. It is for first-run local development only. Change it in Keycloak immediately and never use it in production.

### 2. Optional: run the API and frontend separately

Generate and securely store one stable master key for encrypting managed AI credentials:

```bash
openssl rand -base64 32
export VERISCAN_AI_MASTER_KEY='<paste and persist the generated value>'
```

Then start the API. These sample values are for local development only. Reuse the same `Security__AiCredentials__MasterKey` after every restart:

```bash
ConnectionStrings__VeriScan='Host=127.0.0.1;Port=5432;Database=veriscan;Username=veriscan;Password=veriscan-local-postgres-change-me' \
ConnectionStrings__Redis='127.0.0.1:6379,password=veriscan-local-redis-change-me' \
Database__AutoMigrate=true \
Security__ApiKey__Pepper='replace-with-at-least-32-bytes-local-only' \
Security__ModerationDigests__ContentPepper='replace-with-a-different-32-byte-content-pepper' \
Security__ModerationDigests__IdempotencyPepper='replace-with-a-different-32-byte-idempotency-pepper' \
Security__AiCredentials__MasterKey="$VERISCAN_AI_MASTER_KEY" \
ExternalAi__AllowedHosts__0='api.openai.com' \
ExternalAi__AllowedPorts__0=443 \
ASPNETCORE_URLS='http://127.0.0.1:5000' \
dotnet run --project apps/api/Api
```

External AI has no outbound-host permission by default. Add self-hosted or third-party targets to `ExternalAi__AllowedHosts` / `ExternalAi__AllowedPorts`, and enforce the same boundary with deployment-level egress controls.

Start the admin console:

```bash
cp apps/admin/.env.example apps/admin/.env.local
pnpm --dir apps/admin dev
```

Open `http://127.0.0.1:5173`. The complete Compose stack seeds the local acceptance account. When services are started separately, reuse the same Keycloak realm or create a user with the appropriate VeriScan roles. Mock data is enabled only when `VITE_API_MODE=mock` is set explicitly; a real-mode or OIDC configuration failure never silently falls back to mock mode.

## Configuration flow

1. Create a draft under **Rules & Library**, configure keywords, common formats, or word combinations and their actions in business language, validate it, and publish the revision.
2. Under **AI Configuration**, choose the protocol, enter the model, service URL, and API key, then test, publish, and activate the configuration.
3. Create a caller under **Applications**, bind a published rule revision, and issue an API key with `moderation:submit` / `moderation:read` scopes.
4. Save a public HTTPS webhook URL on the application detail page, persist the one-time signing secret, run a connectivity test, and only then enable notifications.
5. Copy the plaintext API key only when it is created or rotated. The server stores only its digest and cannot recover it later.

Provider secrets are submitted from the admin console and encrypted with AES-GCM under a separate master key. Read APIs return only a configured/not-configured state. Leaving the key blank while editing preserves it; entering a new key rotates it. Never put provider secrets, application API keys, the API-key pepper, or the encryption master key in logs, `appsettings*.json`, or Git. Production deployments should keep the master key in a Secret Manager, Vault, or KMS and back it up separately from the database.

## Moderation API

### Submit a batch

```bash
curl --request POST 'http://127.0.0.1:5000/api/v1/moderation/batches' \
  --header 'Content-Type: application/json' \
  --header 'X-API-Key: <application-api-key>' \
  --header 'Idempotency-Key: order-comment-20260901-001' \
  --data '{
    "mode": "sync",
    "items": [
      {
        "id": "comment-001",
        "content": "Text to moderate",
        "contentType": "plain_text",
        "language": "en"
      }
    ]
  }'
```

Limits: 1–100 items in synchronous mode and 1–1000 items in asynchronous or automatic mode; at most 64 KiB of UTF-8 per item; and `plain_text` only for now. Synchronous requests return HTTP 200. Asynchronous requests return HTTP 202 with `Location` and `Retry-After`. `results[].decision` is `pass`, `reject`, or `review`. Use a unique `Idempotency-Key` for safely replayable submissions.

`mode` accepts `sync`, `async`, or `auto`. Automatic mode selects synchronous or asynchronous execution from the batch size and estimated AI work. Optional `context.scene` and `context.authorType` values can scope rules:

```json
{
  "mode": "auto",
  "items": [
    {
      "id": "comment-001",
      "content": "Text to moderate",
      "contentType": "plain_text",
      "language": "en",
      "context": { "scene": "comment", "authorType": "member" }
    }
  ]
}
```

### Read a recorded batch

```bash
curl 'http://127.0.0.1:5000/api/v1/moderation/batches/<request-id>' \
  --header 'X-API-Key: <application-api-key>'
```

Cancel an asynchronous batch that has not started:

```bash
curl --request POST \
  'http://127.0.0.1:5000/api/v1/moderation/batches/<request-id>/cancel' \
  --header 'X-API-Key: <application-api-key>' \
  --header 'Idempotency-Key: cancel-order-comment-20260901-001'
```

Cancellation requires an independent `Idempotency-Key`; do not reuse the submission key. Replaying the same cancellation key is safe, while using it for another batch returns HTTP 409.

Only `async` requests and `auto` requests that actually enter the queue emit terminal webhooks. Synchronous requests never emit them. See [Webhook integration and delivery](docs/WEBHOOKS.md) for event schemas, signature verification, at-least-once semantics, and enable/disable behavior.

An API key can read records only for its own application. `/healthz` is the liveness endpoint; `/readyz` verifies PostgreSQL connectivity and pending migrations.

## Load test results (2026-09-02)

### Environment and scope

- Apple M1 Max, 10 logical processors, 64 GB RAM, Docker Desktop.
- .NET 10.0.11, PostgreSQL 16.10, Redis 7.4.5.
- Only PostgreSQL and Redis ran in Docker. The API ran as a local Release process with the Production environment and Warning logging, avoiding API-container scheduling effects.
- PostgreSQL and Redis were exposed on loopback ports `35432` and `36379`. The load used 4 temporary applications and 16 API keys in round-robin order while retaining the default rate limits.
- HTTP figures are client-observed end-to-end results including API-key validation, JSON, rule evaluation, Redis / PostgreSQL, and moderation-record persistence.
- No external AI provider was active, so these results do not measure provider latency, provider rate limits, or real model throughput.

### In-process rule-engine baseline

10,000 rules and 100,000 measured iterations, run three times. The table reports the run with median throughput:

| Metric             |        Result |
| ------------------ | ------------: |
| Policy compilation |     27.847 ms |
| Mean latency       |      3.765 µs |
| P95                |      4.375 µs |
| P99                |      4.875 µs |
| Throughput         | 265,583 ops/s |
| Allocation         |    1,432 B/op |

This baseline measures only in-process rule evaluation. It excludes HTTP, databases, authentication, AI, and network latency.

### Local-API HTTP baseline

The harness first sent 200 warm-up requests. Each stable profile then ran three times; the table reports the run with median successful throughput. Every request persisted records to PostgreSQL and produced Outbox and operational facts:

| Profile                  | Requests × items | Concurrency |                     Successful throughput | Client P95 |       P99 | Outcome                                         |
| ------------------------ | ---------------: | ----------: | ----------------------------------------: | ---------: | --------: | ----------------------------------------------- |
| Trusted hard reject      |        1,920 × 1 |          32 |                       1,066.64 requests/s |   43.39 ms |  51.53 ms | 1,920 HTTP 200, zero errors                     |
| Mixed rule batch         |         480 × 10 |          16 |        627.60 batches/s; 6,275.96 items/s |   35.88 ms |  39.79 ms | 480 HTTP 200; 4,320 reject and 480 review       |
| Multi-app burst overload |        2,400 × 1 |         128 | 4,542.45 attempts/s; 1,075.05 successes/s |   73.81 ms | 140.90 ms | 568 HTTP 200, 1,832 HTTP 429, no network errors |

The burst latency combines successful responses with fast 429 responses and therefore is only protection evidence, not successful-request latency or capacity. The local API remained alive and both `/healthz` and `/readyz` returned HTTP 200 afterward. The unpublished Outbox backlog briefly reached 536 and drained to zero within three seconds. Ingress, global, application, and API-key limits remained at their defaults.

The local-rules path exceeded the 1,500 items/s target, but this cannot be extrapolated to AI routing, a real provider, or production resources. With no active AI configuration, unresolved rule outcomes conservatively become `review`; real-provider latency, accuracy, cost, and 60-minute soak tests remain target-environment gates.

### Reproduce

Rule engine:

```bash
dotnet run --project tests/performance/RuleEngine.Baseline/RuleEngine.Baseline.csproj \
  --configuration Release -- 10000 100000
```

The HTTP harness persists real moderation records. Run only PostgreSQL and Redis in Docker, start the Release API locally with the configuration shown above, and create temporary API keys belonging to several applications. Revoke them immediately afterward; the script never prints the keys:

```bash
docker compose --env-file infra/.env -f infra/compose.yaml \
  stop veriscan-api veriscan-admin keycloak
docker compose --env-file infra/.env -f infra/compose.yaml \
  up -d --wait postgres redis

VERISCAN_API_KEYS='<key1>,<key2>,...' \
node tests/performance/http-load.mjs hard 1920 32

VERISCAN_API_KEYS='<key1>,<key2>,...' \
node tests/performance/http-load.mjs mixed 480 16

VERISCAN_API_KEYS='<key1>,<key2>,...' \
node tests/performance/http-load.mjs hard 2400 128
```

`VERISCAN_API_KEYS` rotates credentials per request. Spread the keys across applications so that a single key or application's quota does not hide the ingress-concurrency boundary; `VERISCAN_API_KEY` remains supported for one-key tests. An overload run exits with code 1 when expected 429 responses occur, so evaluate `statusCounts`, client errors, and the subsequent health check in the JSON output. Set `VERISCAN_BASE_URL` to override the default `http://127.0.0.1:5000`. Do not run this against production data.

## Build, test, and container images

```bash
dotnet test VeriScan.slnx
dotnet build VeriScan.slnx -c Release
dotnet format VeriScan.slnx --verify-no-changes
pnpm --dir apps/admin test
pnpm --dir apps/admin lint
pnpm --dir apps/admin typecheck
pnpm --dir apps/admin build
pnpm --dir apps/admin format:check
```

```bash
docker build -f apps/api/Dockerfile -t veriscan-api:local .
docker build -f apps/admin/Dockerfile -t veriscan-admin:local \
  --build-arg VITE_OIDC_AUTHORITY=https://identity.example/realms/veriscan \
  --build-arg VITE_OIDC_REDIRECT_URI=https://admin.example/auth/callback .
```

The API image starts the service by default and can also run a one-shot migration job with `dotnet VeriScan.Api.dll --migrate`. Production deployments must inject database, Redis, API-key pepper, and AI credential-encryption secrets from a secret-management system; never bake them into an image or commit them to the repository.

## Project documentation

- [Implementation plan](docs/IMPLEMENTATION_PLAN.md)
- [Acceptance report](docs/ACCEPTANCE_REPORT.md)
- [Technical proposal (Chinese)](VeriScan-CMS-%E6%8A%80%E6%9C%AF%E6%96%B9%E6%A1%88-v2.md)
