# Log Jammer - MVP Plan Map

## Decisions

| Decision | Choice |
|----------|--------|
| Backend | .NET 10 / C# 13 |
| Frontend | React 19 + MUI 7 + Chart.js 4 + TanStack Query 5 |
| ML Model | ONNX Runtime + all-MiniLM-L6-v2 |
| Database | PostgreSQL 17 + pgvector |
| Deployment | docker-compose (single container API+SPA + PostgreSQL) |
| Adapters (MVP) | Elasticsearch/OpenSearch, PostgreSQL, Log files (JSON + regex) |
| Auth | None (internal tool) |
| Retention | 30 days occurrences / error library indefinite |

---

## Phase Overview

| # | Phase | Plan File | Status |
|---|-------|-----------|--------|
| 1 | Foundation & Project Structure | [phase1-foundation.done.md](phase1-foundation.done.md) | done |
| 2 | Data Source Adapters | [phase2-adapters.done.md](phase2-adapters.done.md) | done |
| 3 | Error Processing Pipeline | [phase3-pipeline.done.md](phase3-pipeline.done.md) | done |
| 4 | ML Classification | [phase4-ml-classification.done.md](phase4-ml-classification.done.md) | done |
| 5 | Spike Detection & Alerts | [phase5-spike-detection.done.md](phase5-spike-detection.done.md) | done |
| 6 | Frontend - Dashboard | [phase6-frontend-dashboard.done.md](phase6-frontend-dashboard.done.md) | done |
| 7 | Frontend - Configuration | [phase7-frontend-config.done.md](phase7-frontend-config.done.md) | done |
| 8 | Docker & Production Build | [phase8-docker-integration.done.md](phase8-docker-integration.done.md) | done |

---

## Dependency Graph

```
                    ┌───────────┐
                    │  Phase 1  │
                    │Foundation │
                    └─────┬─────┘
                          │
              ┌───────────┼───────────┐
              │           │           │
              ▼           │           ▼
        ┌───────────┐    │     ┌───────────┐
        │  Phase 2  │    │     │  Phase 6  │
        │ Adapters  │    │     │ Dashboard │
        └─────┬─────┘    │     └─────┬─────┘
              │           │           │
              ▼           │           ▼
        ┌───────────┐    │     ┌───────────┐
        │  Phase 3  │    │     │  Phase 7  │
        │ Pipeline  │    │     │  Config   │
        └─────┬─────┘    │     └─────┬─────┘
              │           │           │
        ┌─────┴─────┐    │           │
        │           │    │           │
        ▼           ▼    │           │
  ┌───────────┐ ┌───────────┐       │
  │  Phase 4  │ │  Phase 5  │       │
  │    ML     │ │  Spikes   │       │
  └─────┬─────┘ └─────┬─────┘       │
        │              │             │
        └──────┬───────┘             │
               │                     │
               └──────────┬──────────┘
                          │
                          ▼
                    ┌───────────┐
                    │  Phase 8  │
                    │  Docker   │
                    └───────────┘
```

---

## Execution Order

### Sequential (critical path - backend)
```
Phase 1 → Phase 2 → Phase 3 → Phase 4 (parallel with 5)
                              → Phase 5 (parallel with 4)
```

### Sequential (frontend track)
```
Phase 1 → Phase 6 → Phase 7
```

### Final
```
Phase 4 + Phase 5 + Phase 7 → Phase 8
```

---

## Parallel Work Streams

Two independent tracks can run simultaneously after Phase 1:

| Stream | Phases | Focus |
|--------|--------|-------|
| **Backend** | 1 → 2 → 3 → 4+5 | API, adapters, pipeline, ML, spike detection |
| **Frontend** | 1 → 6 → 7 | React dashboard, configuration UI |

Frontend (Phase 6) can start with mock data as soon as Phase 1 delivers the API skeleton. It integrates real APIs incrementally as backend phases land.

Phases 4 (ML) and 5 (Spikes) are independent of each other and can be built in parallel once Phase 3 is done.

---

## Risks & Open Items

| Risk | Impact | Mitigation | Relevant Phase |
|------|--------|------------|----------------|
| ONNX tokenizer integration in .NET | Could delay ML classification | Prototype early; fallback to AllMiniLmL6V2Sharp or custom export | Phase 4 |
| Log file adapter complexity (rotation, regex) | Scope creep | Ship JSON lines first, add regex in a follow-up | Phase 2 |
| pgvector performance at 100K vectors | Slow nearest-neighbor search | Benchmark early; fallback to in-memory HNSW | Phase 4 |
| Real-time dashboard updates | UX feels stale | Polling for MVP (5s interval); add SSE later | Phase 6 |
| SmartComponents.LocalEmbeddings goes stable | Could simplify ML significantly | Monitor; easy swap via IEmbeddingProvider interface | Phase 4 |
