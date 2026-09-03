# Transaction Aggregation API

Lightweight .NET 10 service that ingests financial transactions, aggregates them per-customer and category, and exposes an API. The repository contains:

## Architecture

- src/TransactionAggregation.Api — ASP.NET Core API, Minimal API routes, Scalar/OpenAPI, health checks
- src/TransactionAggregation.Messaging — background services for transaction events(Kafka consumer)
- src/TransactionAggregation.ExternalPublisher — background services for transaction events (Kafka publisher)
- src/TransactionAggregation.Infrastructure — persistence and data sources
- src/TransactionAggregation.Application — business logic and options, MediatR commands/queries/handlers, categorization
- src/TransactionAggregation.Domain — domain models, entities and enums

This README documents how to build and run locally and with Docker, and which environment variables are required.

Prerequisites
- .NET 10 SDK (match project TFM)
- Docker Engine + Docker Compose
- (Windows) Add your user to the `docker-users` group if you plan to run Docker Desktop as non-admin

Quick local development

1. Build the solution from the repository root:

   dotnet build TransactionAggregation.slnx

2. Run database migrations (ensure a connection string is available via environment variable or appsettings):

   # From the solution root
   dotnet ef database update --project src/TransactionAggregation.Infrastructure --startup-project src/TransactionAggregation.Api

   The design-time DbContext factory reads the connection string from the environment variable `Storage__PostgresConnectionString` or from appsettings.json under `Storage:PostgresConnectionString`.

Running with Docker (recommended)

The repo includes a multi-stage root Dockerfile and a docker-compose.yml that brings up:
- api — the ASP.NET Core API (project: src/TransactionAggregation.Api)
- external-publisher — a worker that publishes sample transactions to Kafka (project: src/TransactionAggregation.ExternalPublisher)
- postgres — PostgreSQL 16 (data persisted to a named volume)
- kafka — Apache Kafka broker
- kafka-ui — web UI for inspecting the Kafka cluster
- postgres — PostgreSQL 16 (data persisted to a named volume)
- kafka — Apache Kafka broker
- kafka-ui — web UI for inspecting the Kafka cluster

Start the whole stack:

   docker-compose up --build

Environment variables

docker-compose.yml already supplies sane defaults for local development. Important variables you may want to review:

- Storage__PostgresConnectionString — Postgres connection string used by the API and EF tooling. Example:
  "Host=postgres;Port=5432;Database=transaction_aggregation;Username=postgres;Password=postgres"
- Kafka__BootstrapServers — Kafka bootstrap server(s), default `kafka:9092` in compose
- Kafka__Topic — topic used by publisher and consumer (default `customer-transactions`)
- Kafka__ConsumerGroupId — consumer group id (recommended to set a stable value to avoid "Local: Unknown group" errors). The docker-compose and Dockerfile set sensible defaults; override with env vars if you need a different group.
- Kafka__ClientId — client id used to identify the consumer/publisher client. The compose/Dockerfile may set a ClientId; set `Kafka__ClientId` to a stable value to make logs and metrics easier to correlate.

Notes and troubleshooting

- If the Docker build reports missing .csproj files, it is frequently caused by path/casing mismatches. The root Dockerfile (and the Api Dockerfile) copy the `src/` tree before running `dotnet restore` to avoid this problem.
- There is a small typo in the domain folder name: `src/TransactionAggregtion.Domain` (missing an "a" in Aggregation). This is intentional in the repository but may surprise tooling or hand-written paths; consider renaming the folder and updating project references if you want consistent naming.
- If the Kafka consumer throws a `Local: Unknown group` error, ensure `Kafka__ConsumerGroupId` is configured (either via docker-compose environment or appsettings). The consumer code will also generate a default group id when none is provided, but a stable configured id is recommended for predictable group behavior. If you updated `Kafka__ConsumerGroupId` or `Kafka__ClientId` in Docker/Docker Compose, verify the values are present in the environment for both the api and external-publisher services so consumer and publisher use the intended IDs.
- The design-time DbContext factory will throw a clear error when no Postgres connection string is found — set `Storage__PostgresConnectionString` in your environment or add `Storage:PostgresConnectionString` to appsettings.

Common commands

- Run API locally: dotnet run --project src/TransactionAggregation.Api
- Run unit tests: dotnet test tests/TransactionAggregation.UnitTests
- Build images manually: docker build -f Dockerfile -t txagg:latest .

Contact / contributions

This repository is maintained by the project author. Open issues or PRs on the repository for changes.

