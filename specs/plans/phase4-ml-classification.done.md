# Phase 4: ML Classification (Embeddings + Auto-Tagging)

**Status:** draft
**Depends on:** Phase 3
**Blocks:** Phase 8
**Parallel with:** Phase 5

---

## Goal

Integrate ONNX Runtime + all-MiniLM-L6-v2 for semantic classification of new errors.

---

## Tasks

### 4.1 IEmbeddingProvider interface + ONNX implementation
- Interface: `GenerateEmbeddingAsync(text) -> float[]`, `ComputeSimilarity(a, b) -> float`
- Export all-MiniLM-L6-v2 + tokenizer to ONNX format
- NuGet: `Microsoft.ML.OnnxRuntime`
- Load model on startup, generate 384-dim embeddings
- Benchmark: must be < 50ms per embedding on CPU

### 4.2 pgvector integration
- Store embedding vectors in `known_errors.embedding_vector` column
- Nearest-neighbor search via pgvector (cosine similarity)
- Index: IVFFlat or HNSW on embedding column

### 4.3 Classification pipeline (Stage 3 from requirements)
- For new fingerprints:
  1. Generate embedding for error message + stack trace
  2. Nearest-neighbor search against existing error group embeddings
  3. Similarity threshold (configurable, default cosine > 0.85):
     - Above → suggest merge into existing group, inherit tags
     - Below → create new error group
  4. Auto-tag via tag centroids (average embedding per tag)
  5. Low-confidence → add to classification queue

### 4.4 Tag centroid management
- Compute and cache average embedding per tag
- Recalculate when user corrects classifications
- Store centroids in database

### 4.5 Classification queue
- Errors the system couldn't classify with high confidence
- API endpoints: list queue, approve/reject suggested tags, manual tag assignment

### 4.6 User-driven learning loop
- When user corrects tags: update error group, recalculate tag centroid
- Override system: tag override, severity override, status override, classification pin

### 4.7 Tests
- Embedding generation tests (model loads, produces 384-dim vectors)
- Similarity calculation tests
- Classification pipeline integration tests
- Tag centroid calculation tests

---

## Deliverable

New errors are automatically embedded, classified via nearest-neighbor, auto-tagged. Unclassified errors go to review queue. Users can correct and the system learns.

---

## Risks

- **Tokenizer integration**: Need to resolve AllMiniLmL6V2Sharp vs custom ONNX tokenizer export. Prototype early.
- **pgvector performance**: Validate nearest-neighbor search with 100K vectors. Benchmark IVFFlat vs HNSW.
- **Model size in Docker image**: ~90 MB (fp32) or ~43 MB (fp16). Budget for this in the image.

---

## Acceptance Criteria

- [ ] ONNX model loads on startup without errors
- [ ] Embedding generation < 50ms per error on CPU
- [ ] Embeddings are 384-dimensional float vectors
- [ ] pgvector stores and retrieves embeddings correctly
- [ ] Nearest-neighbor search returns similar errors (cosine similarity)
- [ ] New errors above similarity threshold merge into existing groups
- [ ] New errors below threshold create new groups with auto-tags
- [ ] Tag centroids computed and updated on classification corrections
- [ ] Classification queue API lists unclassified errors with suggested tags
- [ ] User can approve/reject/manually tag from the queue
- [ ] Overrides persist across reclassification runs
