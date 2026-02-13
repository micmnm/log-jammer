# Phase 8: Docker, Integration & Polish

**Status:** draft
**Depends on:** Phase 4, Phase 5, Phase 7
**Blocks:** nothing (final phase)

---

## Goal

Containerize everything, run end-to-end tests, validate performance, finalize documentation.

---

## Tasks

### 8.1 Dockerfile
- Multi-stage build:
  1. Stage 1: Build .NET app (`dotnet publish`)
  2. Stage 2: Build React app (`npm run build`)
  3. Stage 3: Slim runtime image (ASP.NET runtime + static frontend files)
- Bundle ONNX model (all-MiniLM-L6-v2) in the image
- Health check instruction in Dockerfile

### 8.2 docker-compose.yml (production-ready)
- **App container**: .NET API serving React static files + background processing
- **PostgreSQL container**: with pgvector extension enabled
- Volume: PostgreSQL data persistence
- Volume: log files mount (for log file adapter)
- Environment variables: DB connection string, poll budgets, model path, retention days
- Networking: internal network between app + db

### 8.3 End-to-end testing
- Spin up full stack via docker-compose
- Automated test script:
  1. Configure a data source via API
  2. Ingest sample data (seed ES/PG/log files with test errors)
  3. Wait for processing cycle
  4. Verify: fingerprints created, embeddings generated, tags assigned
  5. Trigger a spike, verify alert fires
  6. Verify dashboard renders data correctly

### 8.4 Performance validation
- Load test: 10k+ logs/minute ingestion rate
- Verify < 30s end-to-end processing latency
- Verify embedding generation < 50ms per error on CPU
- Memory profiling under sustained load
- Verify pgvector nearest-neighbor search < 10ms with 100K vectors

### 8.5 Error handling & resilience
- Adapter connection retry logic (exponential backoff)
- Graceful handling of data source unavailability (log warning, skip, retry next cycle)
- Processing pipeline error isolation (one source failing doesn't block others)
- Startup resilience: app starts even if a data source is temporarily unavailable

### 8.6 API documentation
- Swagger/OpenAPI spec finalized and reviewed
- Update `specs/definition-api.md` with all endpoints
- Update `specs/definition-dto.md` with all models

### 8.7 README & getting started
- How to run with `docker-compose up`
- How to configure your first data source
- Environment variables reference
- Troubleshooting common issues

---

## Deliverable

Production-ready MVP: `docker-compose up` starts the full stack, configured and ready to monitor.

---

## Acceptance Criteria

- [ ] `docker-compose up` starts app + PostgreSQL, both healthy
- [ ] React frontend served from the app container
- [ ] ONNX model loads correctly inside the container
- [ ] E2E test passes: data source → ingestion → fingerprint → classification → spike → alert → dashboard
- [ ] 10k+ logs/minute sustained ingestion without degradation
- [ ] Processing latency < 30s end-to-end
- [ ] Embedding generation < 50ms on CPU
- [ ] Memory stays stable under sustained load (no leaks)
- [ ] Failed data source doesn't crash the pipeline
- [ ] Swagger UI serves complete API documentation
- [ ] `definition-api.md` and `definition-dto.md` are up to date
- [ ] README provides working quickstart instructions
