# Log Jammer

Proactive log monitoring with ML-based error classification.


## Overview

Log Jammer is a monitoring application that detects significant events in applications by analyzing structured logs. It uses a local embedding-based ML model to automatically classify, group, and fingerprint errors -- then detects spikes, new error types, and recurring issues to enable faster incident response.

All ML inference runs locally on CPU with no external API calls required.

## Features

- **Data source adapters** -- pluggable pull-based ingestion from Elasticsearch, PostgreSQL, and log files with adaptive sampling
- **Error fingerprinting** -- configurable field-based hashing to group errors by type, with automatic enrollment into a known error library
- **ML classification** -- local embedding model (all-MiniLM-L6-v2 via ONNX Runtime) for automatic error categorization using pgvector nearest-neighbor search
- **Auto-tagging** -- tag centroids assign labels to new errors; low-confidence results are queued for user review
- **User-driven learning loop** -- manual tag corrections recalculate centroids so the system improves over time
- **Spike detection** -- per-error-group threshold rules (absolute count, percentage increase, standard deviation) with configurable windows and lookback periods
- **Correlated spike alerts** -- detects when multiple error groups spike simultaneously within the same data source
- **Alert lifecycle** -- capped escalation model (max 5 notifications) with firing, suppressed, acknowledged, and resolved states
- **Classification review queue** -- browse, approve, or reject suggested tags for unclassified errors
- **React dashboard** -- real-time monitoring UI with error groups, alerts feed, classification queue, and full configuration management

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | .NET 10 / C# 13 |
| Frontend | React 19, TypeScript 5.9, MUI 7, Chart.js 4 |
| Database | PostgreSQL 17 + pgvector |
| ML Runtime | ONNX Runtime (CPU) |
| Embedding Model | all-MiniLM-L6-v2 (384 dimensions) |
| API Docs | Scalar (OpenAPI) |
| Containerization | Docker Compose |

## Project Structure

```
src/
  LogJammer.Core/            Domain entities, enums, interfaces, models
  LogJammer.Infrastructure/  EF Core, adapters, repositories, ML pipeline
  LogJammer.Api/             REST API, controllers, services, DTOs
  LogJammer.Tests/           Integration tests (Testcontainers)
  frontend/                  React SPA (Vite + MUI + TanStack Query)
```

## Getting Started

### Quick Start (Docker)

```bash
docker compose up
```

This builds a single container with both the .NET API and the React frontend, plus a PostgreSQL database with pgvector.

- Application: `http://localhost:5000`
- Scalar API docs: `http://localhost:5000/scalar/v1`
- OpenAPI spec: `http://localhost:5000/openapi/v1.json`
- Health check: `http://localhost:5000/healthz`

The ONNX embedding model (~90MB) is downloaded automatically on first startup and persisted in a named Docker volume.

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | *(set in compose)* | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` for dev CORS and Scalar UI |
| `ASPNETCORE_URLS` | `http://+:8080` | Listening URL inside the container |

The `docker-compose.override.yml` sets `ASPNETCORE_ENVIRONMENT=Development` automatically when using `docker compose up` locally.

### Volumes

| Volume | Container Path | Purpose |
|--------|---------------|---------|
| `pgdata` | `/var/lib/postgresql/data` | PostgreSQL data persistence |
| `models` | `/app/models` | ONNX model cache (avoids re-download) |
| `./logs` | `/app/logs` | Log files for the log file adapter |
| `./data` | `/app/data` | Config data (dev override only) |

## API Overview

| Group | Endpoints | Description |
|-------|-----------|-------------|
| Health | 2 | Application and infrastructure health checks |
| Data Sources | 8 | CRUD + connection test, schema discovery, sampling |
| Fingerprint Configs | 5 | CRUD for per-data-source fingerprint field configs |
| Error Groups | 5 | List, detail, occurrence history, status/severity updates |
| Alerts | 5 | List, acknowledge, history, correlated spike alerts |
| Spike Detection Rules | 5 | CRUD for per-group and global threshold rules |
| Tags | 5 | CRUD for classification tags |
| Configuration | 2 | Get/update classification config (thresholds, etc.) |
| Classification Queue | 4 | Browse pending items, approve/reject suggested tags |

Full endpoint reference: [specs/definition-api.md](specs/definition-api.md)

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Log Jammer Container                   │
│                                                          │
│  ┌─────────────┐    ┌──────────────┐    ┌────────────┐  │
│  │  Scheduler   │───>│  Adapter     │───>│  Parser    │  │
│  │  (per source)│    │  (ES/PG/File)│    │            │  │
│  └─────────────┘    └──────────────┘    └─────┬──────┘  │
│                                                │         │
│                                          ┌─────▼──────┐  │
│                                          │ Fingerprint │  │
│                                          │ Calculator  │  │
│                                          └─────┬──────┘  │
│                                                │         │
│               ┌────────────────────────────────┤         │
│               │ Known?                         │ New?    │
│               ▼                                ▼         │
│  ┌────────────────┐              ┌──────────────────┐   │
│  │ Update Counts  │              │  ML Classifier   │   │
│  └───────┬────────┘              │  (ONNX Runtime)  │   │
│          │                       └────────┬─────────┘   │
│          │                                │              │
│          └────────────┬───────────────────┘              │
│                       ▼                                  │
│              ┌─────────────────┐                         │
│              │ Spike Detector  │                         │
│              │ (per group)     │                         │
│              └────────┬────────┘                         │
│                       ▼                                  │
│              ┌─────────────────┐    ┌────────────────┐  │
│              │  Alert Manager  │───>│   REST API     │  │
│              │                 │    │  + React SPA   │  │
│              └─────────────────┘    └────────────────┘  │
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │              PostgreSQL + pgvector                │   │
│  └──────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

## Development

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- PostgreSQL 17 + pgvector **or** Docker

### Option A: Local PostgreSQL (no Docker needed for the API)

Install PostgreSQL 17 and pgvector via Homebrew:

```bash
brew install postgresql@17
brew install pgvector
brew services start postgresql@17
```

Create the database and user:

```bash
createuser -s logjammer 2>/dev/null; psql -U logjammer -d postgres -c "ALTER USER logjammer PASSWORD 'logjammer';" 2>/dev/null
createdb -U logjammer logjammer 2>/dev/null
createdb -U logjammer logjammer_test 2>/dev/null
psql -U logjammer -d logjammer -c "CREATE EXTENSION IF NOT EXISTS vector;"
psql -U logjammer -d logjammer_test -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

Run the backend and frontend separately:

```bash
# Terminal 1: API (auto-migrates on startup)
dotnet run --project src/LogJammer.Api

# Terminal 2: Frontend dev server (hot reload)
cd src/frontend && npm install && npm run dev
```

The Vite dev server runs at `http://localhost:5173` and proxies `/api` requests to the backend at `http://localhost:5000`.

### Option B: Docker for the database only

```bash
docker compose up db -d
dotnet run --project src/LogJammer.Api
cd src/frontend && npm run dev
```

### Option C: Full Docker stack (production build)

```bash
docker compose up
```

This builds a multi-stage Docker image (Node 22 + .NET SDK 10 + ASP.NET 10 runtime) that serves both the API and the React SPA from a single container.

### Build

```bash
# Backend
dotnet build src/LogJammer.slnx

# Frontend
cd src/frontend && npm run build
```

### Test

**Backend (with Docker):** tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up ephemeral PostgreSQL containers:

```bash
dotnet test src/LogJammer.slnx
```

**Backend (with local PostgreSQL):** set `TEST_USE_LOCAL_DB=true` to run integration tests against a local database:

```bash
TEST_USE_LOCAL_DB=true dotnet test src/LogJammer.slnx
```

You can also override the test connection string:

```bash
TEST_USE_LOCAL_DB=true TEST_CONNECTION_STRING="Host=localhost;Port=5432;Database=logjammer_test;Username=logjammer;Password=logjammer" dotnet test src/LogJammer.slnx
```

When using local DB mode, Elasticsearch adapter tests are skipped if Docker is not available.

**Frontend:**

```bash
cd src/frontend && npm test
```

## License

[MIT](LICENSE)
