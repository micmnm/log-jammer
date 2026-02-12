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

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | .NET 10 / C# 13 |
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
```

## Getting Started

```bash
docker-compose up
```

- API: `http://localhost:5000`
- Scalar API docs: `http://localhost:5000/scalar/v1`
- OpenAPI spec: `http://localhost:5000/openapi/v1.json`
- Health check: `http://localhost:5000/api/health`

The ONNX embedding model is downloaded automatically on first startup.

## API Overview

| Group | Endpoints | Description |
|-------|-----------|-------------|
| Health | 2 | Application and infrastructure health checks |
| Data Sources | 8 | CRUD + connection test, schema discovery, sampling |
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
│              │                 │    │   (Dashboard)  │  │
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
- Docker (for PostgreSQL + pgvector)

### Build

```bash
dotnet build src/LogJammer.slnx
```

### Test

```bash
dotnet test src/LogJammer.slnx
```

Tests use [Testcontainers](https://dotnet.testcontainers.org/) and require Docker to be running.

## License

[MIT](LICENSE)
