# Phase 1: Foundation & Project Structure

**Status:** draft
**Depends on:** nothing
**Blocks:** Phase 2, Phase 6

---

## Goal

Scaffold the solution, set up database, define core domain models and API skeleton.

---

## Tasks

### 1.1 Solution structure
- Create .NET 8 solution with projects:
  - `LogJammer.Api` - ASP.NET Core Web API (host)
  - `LogJammer.Core` - Domain models, interfaces, shared logic
  - `LogJammer.Infrastructure` - Database, adapters, ML integration
  - `LogJammer.Tests` - Unit + integration tests
- Set up `docker-compose.yml` (app + PostgreSQL + pgvector)
- Add `.editorconfig`, `Directory.Build.props` for consistent settings

### 1.2 Database schema & migrations
- Install EF Core + Npgsql + pgvector extension
- Create initial migration with tables:
  - `data_sources` - connection config, poll interval, schema mapping, adapter type
  - `fingerprint_configs` - fields per source that compose the fingerprint
  - `known_errors` - the error library (fingerprint hash, representative message, embedding vector, tags, severity, status, occurrence counts, timestamps)
  - `error_occurrences` - time-bucketed occurrence counts per error group
  - `tags` - tag definitions (name, type: auto/user, color)
  - `error_tags` - many-to-many: error groups to tags
  - `alerts` - alert history with lifecycle state (firing, suppressed, acknowledged, resolved)
  - `user_overrides` - manual classification overrides
  - `classification_queue` - errors awaiting user review
- Seed default tags (timeout, auth-failure, database, oom, network, etc.)

### 1.3 Core domain models (C# classes)
- DTOs matching the database schema
- Enums: `ErrorSeverity`, `ErrorStatus`, `AlertStatus`, `AdapterType`, `ThresholdType`
- Interfaces: `IDataSourceAdapter`, `IEmbeddingProvider`, `ISpikeDetector`

### 1.4 API skeleton
- Health check endpoint
- Basic CRUD endpoints structure (empty controllers for data sources, error groups, alerts, tags, configuration)
- Swagger/OpenAPI setup

### 1.5 Tests
- Test project setup with xUnit
- Database integration test infrastructure (Testcontainers for PostgreSQL)

---

## Deliverable

Running API with health check, PostgreSQL with schema, `docker-compose up` works.

---

## Acceptance Criteria

- [ ] `docker-compose up` starts API + PostgreSQL containers
- [ ] Health check endpoint returns 200
- [ ] Database migrations run automatically on startup
- [ ] Swagger UI accessible at `/swagger`
- [ ] All tables created with correct schema
- [ ] Default tags seeded
- [ ] Test project runs (even if tests are minimal)
