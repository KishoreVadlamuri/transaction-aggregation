- src/TransactionAggregation.Api — ASP.NET Core API, Minimal API routes, Scalar/OpenAPI, health checks
- src/TransactionAggregation.Messaging — background services for transaction events(Kafka consumer)
- src/TransactionAggregation.ExternalPublisher — background services for transaction events (Kafka publisher)
- src/TransactionAggregation.Infrastructure — persistence and data sources
- src/TransactionAggregation.Application — business logic and options, MediatR commands/queries/handlers, categorization
- src/TransactionAggregation.Domain — domain models, entities and enums

# Transaction Aggregation API

.NET 10 service that aggregates customer financial transactions from mock bank / card / payment provider sources, categorizes them, and exposes a JWT-protected API. The full stack also includes Kafka ingest, PostgreSQL, Valkey cache, and Grafana observability.

---

## Quick start

**You only need Docker Desktop** (or Docker Engine + Compose). You do **not** need to install the .NET SDK for this path.

```bash
git clone https://github.com/KishoreVadlamuri/transaction-aggregation.git
cd transaction-aggregation
docker compose up --build
```

Wait until the `api` container is healthy (first start can take a minute while images build). Then open:

| What | URL |
| --- | --- |
| **API docs (Scalar)** | http://localhost:8080/scalar |
| Health | http://localhost:8080/health |
| Grafana dashboard | http://localhost:3000 (`admin` / `admin`) |
| Kafka UI | http://localhost:8081 |

Stop the stack with `Ctrl+C`, or in another terminal:

```bash
docker compose down
```

Add `-v` if you also want to delete the PostgreSQL / Grafana volumes.

### user credentials

| Username | Password |
| --- | --- |
| `appuser` | `AppUser#TxN-2026!` |

### Try it in Scalar (browser)

1. Open http://localhost:8080/scalar
2. Run **`POST /api/v1/auth/login`** with:

```json
{ "username": "appuser", "password": "AppUser#TxN-2026!" }
```

3. Copy the `accessToken` value (the long `eyJ...` string).
4. In Scalar: **Authenticate → Bearer** → paste **only** the token. Do not type the word `Bearer` — Scalar adds that header itself.
5. Run ingest, then aggregations. Example customer: `cust-1001`. Example window: `from=2026-09-01`, `to=2026-09-30`.

### Try it with curl

```bash
TOKEN=$(curl -s -X POST "http://localhost:8080/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"appuser","password":"AppUser#TxN-2026!"}' | jq -r .accessToken)

curl -X POST "http://localhost:8080/api/v1/customers/cust-1001/transactions/ingest" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"from":"2026-07-01T00:00:00Z","to":"2026-08-19T23:59:59Z"}'

curl -i "http://localhost:8080/api/v1/customers/cust-1001/aggregations?from=2026-07-01&to=2026-08-19" \
  -H "Authorization: Bearer $TOKEN"
```

Aggregation responses include `X-Cache: HIT|MISS` and `X-Computation-Ms`.

Health (no auth): `curl http://localhost:8080/health`

---

## What the application does

1. **Ingest** pulls deterministic mock data from Bank, Credit Card, and Payment Provider sources.
2. Transactions are **categorized** with merchant/description rules.
3. The **ExternalPublisher** worker publishes chunks of `FinancialTransaction` from a JSON file to Kafka on a timer; the API consumer categorizes Uncategorized records and persists them.
4. The **aggregation** endpoint joins sources, computes category rollups, and caches the result (Valkey).

| Project | Responsibility |
| --- | --- |
| `TransactionAggregation.Domain` | Entities and enums |
| `TransactionAggregation.Application` | MediatR commands/queries/handlers, categorization |
| `TransactionAggregation.Infrastructure` | Mock sources, Valkey, PostgreSQL store |
| `TransactionAggregation.Messaging` | Kafka consumer |
| `TransactionAggregation.Api` | Minimal API, Scalar/OpenAPI, health, OpenTelemetry |
| `TransactionAggregation.ExternalPublisher` | Standalone feed that publishes JSON chunks to Kafka |
| `TransactionAggregation.UnitTests` | Unit tests |

---

## Run without the full Docker stack

Use this if you want to debug the API in an IDE. **PostgreSQL is still required** (the API applies EF Core migrations on startup). Easiest option: start only Postgres from Compose, then run the API on the host.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet --list-sdks` should show a `10.0.x` entry; `global.json` rolls forward)
- Docker (for PostgreSQL), **or** a local Postgres instance with database `transaction_aggregation`, user/password `postgres`/`postgres`

### Start PostgreSQL

```bash
docker compose up -d postgres
```

### Build, test, run

```bash
dotnet restore TransactionAggregation.slnx
dotnet build TransactionAggregation.slnx -c Release
dotnet test TransactionAggregation.slnx -c Release

dotnet run --project src/TransactionAggregation.Api --launch-profile http
```

- Scalar: http://localhost:5080/scalar
- OpenAPI: http://localhost:5080/openapi/v1.json

Same login as above. Example calls (host port **5080**):

```bash
TOKEN=$(curl -s -X POST "http://localhost:5080/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"appuser","password":"AppUser#TxN-2026!"}' | jq -r .accessToken)

curl -X POST "http://localhost:5080/api/v1/customers/cust-1001/transactions/ingest" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"from":"2026-07-01T00:00:00Z","to":"2026-08-19T23:59:59Z"}'

curl -i "http://localhost:5080/api/v1/customers/cust-1001/aggregations?from=2026-07-01&to=2026-08-19" \
  -H "Authorization: Bearer $TOKEN"
```

### API image only (still needs Postgres)

```bash
docker build --target api -t transaction-aggregation-api .
docker run --rm -p 8080:8080 \
  -e Kafka__Enabled=false \
  -e Cache__ValkeyConnectionString= \
  -e Storage__PostgresConnectionString="Host=host.docker.internal;Port=5432;Database=transaction_aggregation;Username=postgres;Password=postgres" \
  transaction-aggregation-api
```

API: http://localhost:8080/scalar

### External publisher only (needs Kafka)

```bash
dotnet run --project src/TransactionAggregation.ExternalPublisher
```

---

## Compose services

`docker compose up --build` starts:

| Service | Address |
| --- | --- |
| API | http://localhost:8080 |
| External publisher | background worker (no HTTP port); publishes JSON chunks every 10s |
| Valkey | `localhost:6379` |
| PostgreSQL | `localhost:5432` (`transaction_aggregation` / `postgres` / `postgres`) |
| Kafka | `localhost:9092` (advertised as `kafka:9092` on the Compose network) |
| Kafka UI | http://localhost:8081 |
| Prometheus | http://localhost:9090 (scrapes `api:8080/metrics` on the Docker network; open http://localhost:8080/metrics from your browser) |
| Grafana | http://localhost:3000 |

---

## Observability

After `docker compose up --build`:

1. Open Grafana: http://localhost:3000 (`admin` / `admin`, or anonymous Viewer).
2. Open the home dashboard **Transaction Aggregation — Observability**.
3. Generate traffic (login → ingest → aggregations) so panels populate.
4. Optional: Prometheus targets at http://localhost:9090/targets — wait until the API job is **UP**.
5. Optional: raw metrics at http://localhost:8080/metrics (this is the host URL).

The Prometheus targets page shows `http://api:8080/metrics`. That hostname exists **only inside Docker**. Opening it in your browser will fail with “can't reach this page”. Use http://localhost:8080/metrics instead. A green **UP** state on `/targets` means Prometheus is scraping successfully.

The API exports:

| Signal | How | Where to look |
| --- | --- | --- |
| Metrics | Prometheus scrape of `/metrics` | Grafana dashboard **Transaction Aggregation — Observability** |
| Traces | OTLP → Tempo (when `Observability:OtlpEndpoint` is set) | Grafana → Explore → Tempo |
| Health | `GET /health` | Compose healthcheck / Prometheus `up` |

Business metrics (meter `TransactionAggregation`):

- `txn_agg_aggregations` — aggregation requests labeled by `cache=hit|miss`
- `txn_agg_aggregation_duration` — compute latency histogram (misses)
- `txn_agg_aggregation_transactions` — transactions rolled up on misses
- `txn_agg_ingest_requests` / `txn_agg_ingested_transactions` — ingest throughput

Plus ASP.NET Core HTTP, HttpClient, and .NET runtime instrumentation.

| Setting | Purpose | Default |
| --- | --- | --- |
| `Observability:Enabled` | Master switch for OpenTelemetry | `true` |
| `Observability:PrometheusEnabled` | Expose `/metrics` | `true` |
| `Observability:OtlpEndpoint` | OTLP endpoint (e.g. `http://tempo:4317`) | empty |
| `Observability:OtlpProtocol` | `grpc` or `http/protobuf` | `grpc` |

---

## API surface

All business endpoints are under `/api/v1/...`. Health and metrics are unversioned.

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| `POST` | `/api/v1/auth/login` | Anonymous | Login → JWT |
| `POST` | `/api/v1/customers/{customerId}/transactions/ingest` | JWT | Ingest + categorize + Kafka publish |
| `GET` | `/api/v1/customers/{customerId}/transactions` | JWT | Read stored transactions |
| `GET` | `/api/v1/customers/{customerId}/aggregations` | JWT | Multi-source aggregation |
| `GET` | `/api/v1/customers/{customerId}/aggregations/categories/{category}` | JWT | Transactions for one category |
| `GET` | `/health` | Anonymous | Health probe |
| `GET` | `/metrics` | Anonymous | Prometheus scrape |

---

## Configuration

| Setting | Purpose | Default |
| --- | --- | --- |
| `Aggregation:CacheTtlSeconds` | Aggregation cache TTL | `120` |
| `Kafka:BootstrapServers` | Kafka brokers | see `appsettings.json` |
| `Kafka:Topic` | Transaction event topic | see `appsettings.json` |
| `Kafka:ConsumerGroupId` | Consumer group id | see `appsettings.json` |
| `Kafka:ClientId` | Kafka client id | see `appsettings.json` |
| `Cache:ValkeyConnectionString` | Valkey endpoint; empty = in-process memory | empty |
| `Storage:PostgresConnectionString` | PostgreSQL (required) | empty (localhost in Development) |
| `Jwt:SigningKey` | JWT signing key (min 32 chars) | see `appsettings.json` |
| `ServiceAccount:Username` | Service account username | `appuser` |
| `ServiceAccount:PasswordHash` | ASP.NET Identity password hash | see `appsettings.json` |

Environment variables use `__`, for example:

```bash
export Storage__PostgresConnectionString="Host=localhost;Port=5432;Database=transaction_aggregation;Username=postgres;Password=postgres"
export Cache__ValkeyConnectionString=localhost:6379
export Kafka__BootstrapServers=localhost:9092
```

### Authentication

One service account. No roles — any valid token can call every protected endpoint. Config stores **only** `PasswordHash`, never the plaintext password. Login verifies the submitted password against that hash.

| Account | Login password | Access |
| --- | --- | --- |
| `appuser` | `AppUser#TxN-2026!` (must match `PasswordHash`) | All protected endpoints |

Generate a new hash (ASP.NET Identity `PasswordHasher`):

```csharp
var hash = new PasswordHasher<object>().HashPassword(new object(), "YourStrongPassword");
```

Set the result as `ServiceAccount:PasswordHash` / `ServiceAccount__PasswordHash`.

### Publisher settings

| Setting | Purpose | Default |
| --- | --- | --- |
| `Publisher:Enabled` | Turn automatic publishing on/off | `true` |
| `Publisher:IntervalSeconds` | Seconds between chunks | `10` |
| `Publisher:ChunkSize` | Records per chunk | `50` |
| `Publisher:DataFilePath` | JSON file of transactions | `Data/financial-transactions.json` |

### PostgreSQL / EF Core

`PostgresTransactionStore` persists ingested transactions. The API applies pending migrations on startup via `Database.MigrateAsync()`.

Migrations live in `src/TransactionAggregation.Infrastructure/Persistence/Migrations`.

```bash
dotnet tool install --global dotnet-ef

export Storage__PostgresConnectionString="Host=localhost;Port=5432;Database=transaction_aggregation;Username=postgres;Password=postgres"

dotnet ef migrations add <MigrationName> \
  --project src/TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj \
  --startup-project src/TransactionAggregation.Api/TransactionAggregation.Api.csproj \
  --output-dir Persistence/Migrations \
  --context TransactionDbContext

dotnet ef database update \
  --project src/TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj \
  --startup-project src/TransactionAggregation.Api/TransactionAggregation.Api.csproj \
  --context TransactionDbContext
```

---

## Design notes

- **JWT auth** protects business routes with a single service account (`appuser`).
- **API v1** is a URL prefix (`/api/v1/...`) via Minimal API `MapGroup` — no Asp.Versioning packages.
- **Mock sources** produce deterministic transactions per customer/date window so tests are stable.
- **Caching** uses Valkey when configured; otherwise an in-process distributed memory cache.
- **Kafka** failures are non-fatal for local development; publish/consume logs warnings and continues.
- **ExternalPublisher** simulates an upstream feed by reading JSON and publishing chunks on a timer.
- **OpenTelemetry** exposes Prometheus `/metrics` and optional OTLP traces for Grafana + Tempo.


