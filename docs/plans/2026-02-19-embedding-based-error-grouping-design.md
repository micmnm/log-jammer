# Embedding-Based Error Grouping at Ingestion Time

## Problem

Log messages that represent the same error get different fingerprint hashes due to structural formatting differences across sources. For example:

```
BusMessageId: 92c850e9-..., CorrelationId: aa96e498-..., Request failed with status code BadGateway(Request host is sherlene-interstation-unhistorically.ngrok-free.dev)
```

```
BusMessageId: "92c850e9-...", BusCorrelationId: "aa96e498-...", "502:BadGateway:Bad Gateway:Request failed with status code BadGateway(Request host is sherlene-interstation-unhistorically.ngrok-free.dev)"
```

These are semantically identical (same error type + same target) but differ in:
- Quotes vs no quotes
- Key name variations (`CorrelationId` vs `BusCorrelationId`)
- Error code prefixes (`502:BadGateway:Bad Gateway:`)

The regex-based `FingerprintNormalizer` strips dynamic values (UUIDs, timestamps) but cannot handle structural formatting differences. The async `ClassificationProcessor` has an embedding-based merge, but it runs too late and often fails to catch these cases.

## Solution

Move embedding-based similarity search into the ingestion pipeline as a fallback when fingerprint hash lookup misses.

## Architecture

### Current Flow

```
Log entry -> SchemaMapper -> FingerprintCalculator (SHA-256 hash) -> exact hash/alias match?
  YES -> increment existing KnownError
  NO  -> create new KnownError + queue for async classification
         (ClassificationProcessor later attempts embedding merge - often misses)
```

### Proposed Flow

```
Log entry -> SchemaMapper -> FingerprintCalculator (SHA-256 hash) -> exact hash/alias match?
  YES -> increment existing KnownError (fast path, unchanged)
  NO  -> compute embedding -> pgvector similarity search against existing KnownErrors
    similarity >= threshold -> group with existing KnownError + create FingerprintAlias
    similarity < threshold  -> create new KnownError with embedding pre-computed
                               + queue for tag classification only
```

Embedding computation only runs for new, unrecognized fingerprints. Once a `FingerprintAlias` is created, all future occurrences with that hash take the fast path.

## Components

### 1. LogIngestionPipeline Changes

New dependency: `IEmbeddingProvider` (already registered as singleton).

After fingerprint hash miss + alias miss:
1. Normalize message text (light cleanup for embedding input)
2. Call `IEmbeddingProvider.GenerateEmbeddingAsync()` (~5-20ms on CPU)
3. Run pgvector `CosineDistance()` query: find nearest `KnownError` where `EmbeddingVector IS NOT NULL`
4. If best match similarity >= `IngestionSimilarityThreshold`:
   - Group with existing `KnownError` (increment occurrences, update LastSeen)
   - Create `FingerprintAlias` for the new hash -> target KnownError
5. If no match above threshold:
   - Create new `KnownError` with `EmbeddingVector` pre-populated
   - Add to `ClassificationQueue` for tag suggestions only

### 2. FingerprintNormalizer Enhancements

Light additions to improve embedding input quality (not to fix hash grouping):
- Strip double and single quotes
- Strip key-value label patterns (generalized `\b\w+(?:Id|id):\s*`)
- Strip HTTP status code text (`\d{3}:?\w*:?`)

### 3. ClassificationProcessor Simplification

The merge path (`MergeIntoAsync`) remains as a safety net but rarely triggers. Primary role becomes:
- Tag suggestion via centroid matching (unchanged)
- Auto-tagging high-confidence matches (unchanged)
- Backfill: classifying errors created before this change

### 4. Configuration

New entries in `ClassificationConfig` table:

| Key | Default | Description |
|-----|---------|-------------|
| `IngestionSimilarityThreshold` | `0.80` | Cosine similarity threshold for grouping at ingestion |
| `IngestionSimilarityEnabled` | `true` | Feature flag to disable embedding lookup at ingestion |

## Performance Characteristics

- **Fast path** (hash/alias hit): No change, no embedding computation
- **Slow path** (new fingerprint): +5-20ms for ONNX inference + pgvector query
- **One-time cost**: Once alias is created, future occurrences never hit the slow path again
- The ONNX model is already loaded as a singleton; no cold start after first use

## Files to Modify

- `src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs` — add embedding fallback
- `src/LogJammer.Infrastructure/Pipeline/FingerprintNormalizer.cs` — add quote/label stripping
- `src/LogJammer.Infrastructure/Pipeline/ClassificationProcessor.cs` — simplify (merge becomes safety net)
- `src/LogJammer.Infrastructure/Data/LogJammerDbContext.cs` — seed new config entries
- `src/LogJammer.Tests/Unit/Pipeline/FingerprintNormalizerTests.cs` — new test cases
- `src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs` — embedding fallback tests
