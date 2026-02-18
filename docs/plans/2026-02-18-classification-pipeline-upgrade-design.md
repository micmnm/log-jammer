# Classification Pipeline Upgrade — Design Doc

**Date:** 2026-02-18
**Status:** Approved
**Scope:** Improve ML classification accuracy, add queue clustering, smarter matching, adaptive thresholds, SampleLog mock ES server

---

## Problem Statement

The current classification pipeline has three pain points:

1. **Flat queue** — Similar errors are presented individually, requiring one-by-one triage even when the ML knows they're related
2. **Generic embeddings** — all-MiniLM-L6-v2 treats log messages as plain text, ignoring structured fields (exception type, application, level) that are strong classification signals
3. **Static thresholds** — The 0.85 similarity and 0.7 auto-tag thresholds are hardcoded; no feedback mechanism to tune them based on user behavior

---

## Design

### A. Feature Extraction

New `ErrorFeatureExtractor` service that parses structured features from error data.

**Extracted features:**

| ELK Field | Feature Key | Role in Classification |
|-----------|------------|----------------------|
| `level` | `level` | Structural match — same level = small boost |
| `fields.Application` / `fields.ServiceName` | `application` | Structural match — same app = boost |
| `exception.Type` / `exception.ClassName` | `exceptionType` | Structural match — same exception type = strong boost |
| `fields.SourceContext` / `logger_name` | `logger` | Structural match — same module = boost |
| `message` | `messageTemplate` | Normalize (strip numbers, UUIDs, IPs, timestamps) → template similarity |
| `exception.StackTrace` | `topFrames` | Top 3 non-framework stack frames |

**Extraction strategy:**
- **From schema-mapped fields** when the data source has explicit ELK fields (preferred)
- **From regex parsing** of message + stack trace as fallback (for log file sources)

**Storage:** New `ExtractedFeatures` jsonb column on `KnownError` entity. Free-form dictionary — adding new fields later requires no schema change, just extraction logic + weight config.

### B. Improved Embedding Text Composition

Before sending to all-MiniLM-L6-v2, compose a structured text that includes extracted features:

```
[PaymentService] [NullReferenceException] [Error] Object reference not set to an instance of an object
OrderProcessor.Process OrderController.Submit
```

This gives the embedding model application + exception + level context, producing better semantic similarity than raw message concatenation.

### C. Composite Similarity Scoring

Replace single cosine similarity with a weighted composite score:

```
final_score = EmbeddingWeight   * embedding_cosine_similarity
            + TemplateWeight    * normalized_levenshtein(template_a, template_b)
            + StructuralWeight  * structural_match_score
            + MetadataWeight    * metadata_overlap_score
```

**Structural match scoring:**
- Same `exceptionType` → 0.4
- Same `application` → 0.3
- Same `level` → 0.1
- Same `logger` → 0.2
- Score = sum of matching features / 1.0 (capped at 1.0)

**Metadata overlap scoring:**
- Compare `topFrames` arrays — Jaccard similarity of shared frames

**Default weights:** 0.50 / 0.20 / 0.20 / 0.10 (configurable via Settings).

### D. Pre-Classification Clustering

**Backend:**
- Add `ClusterId` (nullable Guid) column to `ClassificationQueueItem`
- New `ClassificationClusterService`:
  1. After `ClassificationProcessor` generates embeddings and features, run a clustering pass over all unreviewed items
  2. Algorithm: greedy single-linkage — take first unclustered item, find all items within `ClusteringThreshold` composite score, assign same `ClusterId`. Repeat.
  3. Cluster representative = oldest item or highest occurrence count
  4. Re-cluster periodically (every batch or every 5 minutes)

**API changes:**
- `GET /api/classification/queue` response adds `clusterId`, `clusterSize` to each item
- `POST /api/classification/queue/{id}/approve` accepts `applyToCluster: true` — applies tags to all cluster members and marks them reviewed
- `POST /api/classification/queue/{id}/reject` same — applies correction to all cluster members

**Frontend changes:**
- Classification Queue page groups items by `clusterId`
- Each cluster shows the representative card with a badge: "+N similar"
- Expandable: click to see all cluster members
- Approve/Reject buttons apply to the whole cluster by default
- "Classify this one only" escape hatch for edge cases

### E. Adaptive Thresholds (Lightweight)

**Decision tracking:**
- New `ClassificationDecision` entity: `KnownErrorId`, `ClusterId`, `SimilarityScore`, `Decision` (approve/reject), `Timestamp`
- Recorded on every approve/reject action

**Threshold suggestions:**
- Settings UI shows stats: "Your approve average score: X, your reject average: Y"
- Suggests optimal threshold based on decision history
- User manually adjusts — no automatic adjustment (transparent and controllable)
- Requires at least 20 decisions before showing suggestions

### F. SampleLog Mock ES Server

SampleLog gains a mock Elasticsearch HTTP server mode:

**Endpoints:**
- `GET /{index}/_search` — returns generated log documents matching time-range queries
- `GET /{index}/_mapping` — returns realistic ELK index mapping
- `GET /` or `GET /_cluster/health` — health check for connection testing

**Behavior:**
- Generates structured log documents using existing scenario engine
- Configurable index name
- Time-aware: responds to `@timestamp` range queries, gradually produces new documents over time (simulates live ingestion)
- Field structure matches typical ELK:
  ```json
  {
    "@timestamp": "2026-02-18T10:30:00Z",
    "level": "Error",
    "message": "Request POST /api/orders failed",
    "exception": {
      "type": "NullReferenceException",
      "message": "Object reference not set...",
      "stackTrace": "at ..."
    },
    "fields": {
      "Application": "OrderService",
      "SourceContext": "Checkout.OrderProcessor"
    }
  }
  ```
- Multiple error archetypes with variations for realistic classification scenarios

**Integration:** LogJammer's ES adapter connects to `http://localhost:{port}` and polls as if it were a real Elasticsearch instance.

---

## Configuration

### New ClassificationConfig entries

| Key | Default | Description |
|-----|---------|-------------|
| `EmbeddingWeight` | `0.50` | Weight of embedding similarity in composite score |
| `TemplateWeight` | `0.20` | Weight of message template similarity |
| `StructuralWeight` | `0.20` | Weight of structural feature matches |
| `MetadataWeight` | `0.10` | Weight of logger/frame overlap |
| `ClusteringEnabled` | `true` | Pre-cluster queue items |
| `ClusteringThreshold` | `0.70` | Minimum composite score to join a cluster |
| `AdaptiveThresholdEnabled` | `false` | Show adaptive threshold suggestions |

### Settings UI Changes

Classification tab gets:
- **Scoring Weights** section — 4 sliders (EmbeddingWeight, TemplateWeight, StructuralWeight, MetadataWeight) constrained to sum to 1.0
- **Clustering** section — enabled toggle + threshold slider
- **Threshold Tuning** section — decision stats + suggested threshold + manual override

---

## Database Changes

1. `KnownError`: Add `ExtractedFeatures` (jsonb, nullable)
2. `ClassificationQueueItem`: Add `ClusterId` (Guid, nullable)
3. New `ClassificationDecision` table: `Id`, `KnownErrorId`, `ClusterId`, `SimilarityScore`, `Decision`, `CreatedAt`
4. New migration

---

## Existing Settings (Unchanged)

- `SimilarityThreshold` (0.85) — used for merge decisions
- `AutoTagConfidenceThreshold` (0.7) — used for auto-tag assignment
- `MaxSuggestedTags` (3) — max suggested tags per item

---

## Out of Scope

- Per-data-source thresholds
- Automatic threshold adjustment (user always controls manually)
- Generic custom field framework (ELK-native fields only, extensible via ExtractedFeatures jsonb)
- Request context fields (HTTP method, path, status code) — can add later via new extraction rules
