# Log Jammer - ML Embedding Model Selection

## Context

Log Jammer needs a local embedding model for error classification and grouping. Key constraints from the requirements:

- **No GPU** - must run on CPU only
- **Low memory** - single Docker container, model shares resources with the .NET app, PostgreSQL, and the frontend
- **Low latency** - < 50ms per embedding generation (requirement), < 10ms nearest-neighbor search
- **No remote API calls** - fully local, air-gapped capable
- **100K error groups** max in memory
- **.NET 8+ / C#** - must integrate cleanly, ideally via NuGet
- **384-dim vectors** are the sweet spot (stored in pgvector, manageable memory for 100K groups)

## Memory Budget Estimation

With 100K error groups, each storing a 384-dim float32 embedding:

```
100,000 groups x 384 dimensions x 4 bytes = ~150 MB for embeddings alone
+ model weights in memory
+ ONNX Runtime overhead
+ .NET app + PostgreSQL + pgvector
```

The model itself should ideally stay **under 100 MB** in memory to leave room for the rest of the stack in a single container.

---

## Proposal A: ONNX Runtime + all-MiniLM-L6-v2

### What

Run the `all-MiniLM-L6-v2` sentence-transformer model (22M parameters) via `Microsoft.ML.OnnxRuntime` NuGet package. The model is exported to ONNX format from Hugging Face and loaded in-process.

### Key Libraries

| Package | NuGet Downloads | Status |
|---------|----------------|--------|
| `Microsoft.ML.OnnxRuntime` | **8M+** | Actively maintained by Microsoft, v1.24+ |
| `AllMiniLmL6V2Sharp` | Low (~hundreds) | v0.0.3, single maintainer, includes tokenizer + model |

Two integration paths:
1. **AllMiniLmL6V2Sharp** - batteries-included NuGet, bundles tokenizer + ONNX model. Simple API: `embedder.GenerateEmbedding(text)`. Risk: single maintainer, v0.0.3, low adoption.
2. **Roll your own** on top of `Microsoft.ML.OnnxRuntime` - export both model and tokenizer to ONNX (following [yuniko.software approach](https://yuniko.software/hugging-face-tokenizer-to-onnx-model/)). More work upfront, full control.

### Model Specs

| Metric | Value |
|--------|-------|
| Parameters | 22M |
| Model size (fp32) | ~90 MB |
| Model size (fp16) | ~43 MB |
| Embedding dimensions | 384 |
| MTEB score | 56.3 |
| Inference latency (CPU) | ~15ms per 1K tokens |
| Throughput (CPU) | ~14,000 sentences/sec |
| Memory at runtime | ~100-170 MB (model + runtime) |

### Pros

- **Most documented path** for this exact use case (error log embeddings in .NET). Hundreds of blog posts, examples, StackOverflow answers.
- **ONNX Runtime is Microsoft's own** inference engine - 8M+ NuGet downloads, actively maintained, .NET 8 compatible, excellent CPU optimizations.
- **all-MiniLM-L6-v2 is the most widely deployed** small embedding model. 100M+ downloads on Hugging Face. Battle-tested.
- **Good quality for the size** - 384-dim embeddings capture semantic similarity well enough for error grouping (we're comparing stack traces and error messages, not literary nuance).
- **Quantization available** - can go down to ~43 MB with fp16, further with int8 quantization.
- **Model is swappable** - ONNX format means you can later upgrade to a better model (e.g., bge-small-en-v1.5) without changing any code.

### Cons

- **~100-170 MB memory** for model + ONNX Runtime. Largest of the three options.
- **Tokenizer gap** - the ONNX model alone doesn't include the tokenizer. Either use AllMiniLmL6V2Sharp (risky dependency) or export the tokenizer to ONNX yourself (extra setup).
- **MTEB score is dated** (56.3) - the model is from 2021. Newer micro models score better, but for error log similarity it's likely sufficient.
- **Cold start** - loading the model into memory takes 2-5 seconds. Not an issue for a long-running service, but noticeable in tests.
- **AllMiniLmL6V2Sharp is fragile** - v0.0.3, single maintainer, no guarantees it stays maintained.

### Risk Assessment

**Low-medium risk.** The ONNX Runtime itself is rock-solid. The main risk is the tokenizer integration, which is a solvable engineering problem. If AllMiniLmL6V2Sharp breaks, we can vendor the tokenizer or export it to ONNX.

---

## Proposal B: SmartComponents.LocalEmbeddings + bge-micro-v2

### What

Use Microsoft's own `SmartComponents.LocalEmbeddings` library, which wraps ONNX Runtime and ships with `bge-micro-v2` - a quantized BERT embedding model at just **22.9 MB**.

### Key Libraries

| Package | NuGet Downloads | Status |
|---------|----------------|--------|
| `SmartComponents.LocalEmbeddings` | ~19K total | Preview (0.1.0-preview), under `dotnet/smartcomponents` org |
| `Microsoft.ML.OnnxRuntime` (transitive) | 8M+ | Stable |

### Model Specs

| Metric | Value |
|--------|-------|
| Model size (quantized) | **22.9 MB** |
| Embedding dimensions | 384 |
| Inference latency (CPU) | **< 1ms** per embedding |
| Semantic search (100K candidates) | single-digit milliseconds |
| Memory at runtime | **~50-80 MB** (model + runtime) |
| MTEB score | Not formally published (bge-micro is a heavily quantized/distilled variant) |

### Integration

```csharp
// One-liner setup
using var embedder = new LocalEmbedder();

// Generate embedding
var embedding = embedder.Embed("NullReferenceException at UserService.GetUser()");

// Similarity search
var similarity = LocalEmbedder.Similarity(embedding1, embedding2);
```

- Model is auto-downloaded on first build via MSBuild target.
- Tokenizer is included - no separate tokenizer export needed.
- API is purpose-built for .NET - `IDisposable`, clean async patterns.

### Pros

- **Smallest memory footprint** - 22.9 MB model, ~50-80 MB total at runtime. Leaves the most room for PostgreSQL and the app in a single container.
- **Sub-millisecond inference** - the fastest option by far. Well within the <50ms requirement.
- **Batteries included** - tokenizer, model download, ONNX Runtime wiring all handled. Least integration effort.
- **Microsoft's own** - under the `dotnet/` GitHub org. Uses the same ONNX Runtime underneath.
- **384-dim output** - same dimensionality as MiniLM, so pgvector storage is identical.
- **MIT licensed** - no commercial restrictions.

### Cons

- **Preview status** - v0.1.0-preview. Not yet stable. API could change.
- **Low adoption** - ~19K downloads. The original repo (`dotnet-smartcomponents`) was archived Aug 2024; development continues under `dotnet/smartcomponents` but pace is unclear.
- **Embedding quality unknown** - bge-micro-v2 is a heavily quantized model. No published MTEB score. For generic text similarity it's reportedly good, but for the specific domain of error logs and stack traces, it's unproven.
- **Less community knowledge** - very few blog posts or examples. If something breaks, you're reading source code.
- **Model swap is harder** - the library is designed around its bundled model. Swapping to a different ONNX model is possible but not a first-class feature.
- **Build-time model download** - requires internet during build (or manual model placement). Could complicate CI/CD in air-gapped environments.

### Risk Assessment

**Medium risk.** The underlying tech (ONNX Runtime) is solid, but the wrapper library is immature. If it stalls, we'd need to migrate to raw ONNX Runtime (essentially falling back to Proposal A). The embedding quality for error logs specifically is the biggest unknown.

---

## Proposal C: ML.NET TF-IDF + Classical ML (No Neural Network)

### What

Skip neural embeddings entirely. Use ML.NET's built-in `TextFeaturizingEstimator` to produce TF-IDF + n-gram feature vectors, then use classical algorithms (cosine similarity, k-NN, logistic regression) for grouping and classification.

### Key Libraries

| Package | NuGet Downloads | Status |
|---------|----------------|--------|
| `Microsoft.ML` | **10M+** | Stable, v3.0+, actively maintained by Microsoft |
| `Microsoft.ML.Transforms.Text` | Included in ML.NET | Stable |

### How It Works

```
[Error text] → Tokenize → N-grams → TF-IDF weighting → Sparse vector
```

- No pre-trained model file needed. Vocabulary and IDF weights are built from your own data.
- Similarity is computed via cosine similarity on sparse vectors.
- Classification can use ML.NET trainers (SDCA, Naive Bayes, LightGBM) on the TF-IDF features.

### Specs

| Metric | Value |
|--------|-------|
| Model size | **0 MB pre-trained** (vocabulary built from data, typically 1-10 MB) |
| Embedding dimensions | Variable (vocabulary size, typically 5K-50K sparse features) |
| Inference latency (CPU) | **< 1ms** |
| Memory at runtime | **~20-50 MB** (vocabulary + IDF weights) |
| Quality | Good for lexical matching, poor for semantic similarity |

### Pros

- **Smallest possible footprint** - no model file, no ONNX Runtime, no neural network. Just ML.NET (which you may already use for other things).
- **ML.NET is rock-solid** - 10M+ downloads, Microsoft first-party, stable API, .NET 8 native.
- **Fastest inference** - TF-IDF is a simple lookup + multiply. Sub-millisecond.
- **Fully explainable** - you can inspect exactly which terms contribute to similarity. No black box.
- **Domain adaptation is natural** - the vocabulary is built from your own error logs. No pre-training mismatch.
- **No model versioning headaches** - no ONNX model to ship, update, or worry about.
- **Works in air-gapped environments** - nothing to download, ever.

### Cons

- **No semantic understanding** - TF-IDF is purely lexical. `"connection timed out"` and `"socket timeout"` are unrelated in TF-IDF space despite meaning the same thing. This is a **fundamental limitation** for error classification.
- **Cannot auto-tag by meaning** - the requirements call for auto-assigning tags like `timeout`, `auth-failure`, `database` based on semantic similarity to tag centroids. TF-IDF can only do this if the exact keywords appear.
- **Sparse vectors are expensive at scale** - a 30K-dim sparse vector per error group is much larger than a 384-dim dense vector. pgvector doesn't support sparse vectors natively, so you'd need a different storage approach.
- **Cold start problem** - needs a corpus of errors to build the vocabulary. On first run with zero data, the system produces poor features.
- **Nearest-neighbor search is slower** - sparse high-dimensional vectors don't benefit from pgvector's HNSW index. You'd need a different similarity search strategy.
- **Not what the requirements envision** - the requirements explicitly describe embedding-based classification with nearest-neighbor search and tag centroids. TF-IDF would require rethinking several subsystems.

### Risk Assessment

**Low technical risk, high product risk.** ML.NET is bulletproof, but the semantic gap is real. Error logs are messy, full of synonyms and paraphrases. A system that can't recognize `"ECONNREFUSED"` and `"connection refused"` as the same thing will produce poor grouping. This option works best as a **fallback or supplement** to a neural approach, not as the primary classifier.

---

## Comparison Matrix

| Criteria | A: ONNX + MiniLM | B: SmartComponents | C: ML.NET TF-IDF |
|----------|:-:|:-:|:-:|
| **Memory (model)** | ~90 MB (fp32) / ~43 MB (fp16) | ~23 MB | ~1-10 MB |
| **Memory (runtime total)** | ~100-170 MB | ~50-80 MB | ~20-50 MB |
| **Inference latency** | ~15ms | < 1ms | < 1ms |
| **Embedding quality** | Good (MTEB 56.3) | Unknown (likely decent) | Lexical only |
| **Semantic similarity** | Yes | Yes | No |
| **NuGet traction** | ONNX: 8M+ / Wrapper: low | ~19K (preview) | ML.NET: 10M+ |
| **Maintained by** | Microsoft (ONNX) / community (wrapper) | Microsoft (preview) | Microsoft (stable) |
| **Integration effort** | Medium (tokenizer work) | Low (batteries included) | Low-medium |
| **Model swappable** | Yes (any ONNX model) | Limited | N/A |
| **Air-gapped friendly** | Yes (bundle ONNX file) | Needs build-time download | Yes |
| **Fits requirements as-written** | Fully | Fully | Partially (no semantic grouping) |

---

## Recommendation

### Primary: Proposal A (ONNX Runtime + all-MiniLM-L6-v2)

**Why:** It's the safest bet that fully satisfies the requirements. The ONNX Runtime is Microsoft-backed and battle-tested. The model is the most widely deployed small embedding model in existence. The memory cost (~100-170 MB) is manageable in a Docker container with 2-4 GB RAM. The tokenizer gap is a solvable problem (either vendor AllMiniLmL6V2Sharp's tokenizer or export to ONNX).

### With an eye on: Proposal B (SmartComponents.LocalEmbeddings)

**Why watch it:** If SmartComponents.LocalEmbeddings reaches stable release, it becomes the obvious choice - half the memory, 15x faster, zero integration effort. It's worth prototyping with to validate embedding quality on error log data. If the quality holds up for our use case, migrate.

### Fallback layer: Proposal C (TF-IDF)

**Why keep it:** TF-IDF can serve as a **complementary signal** alongside embeddings. Stack traces have strong lexical patterns (`at Namespace.Class.Method()`) where exact matching outperforms semantic similarity. A hybrid approach - TF-IDF for stack trace matching + embeddings for error message similarity - could outperform either alone. ML.NET is free to add since it's already in the .NET ecosystem.

---

## Suggested Architecture for Flexibility

```
IEmbeddingProvider
  - GenerateEmbedding(text: string) → float[]
  - ComputeSimilarity(a: float[], b: float[]) → float

Implementations:
  - OnnxEmbeddingProvider     (Proposal A - primary)
  - SmartComponentsProvider   (Proposal B - experimental)
  - TfIdfEmbeddingProvider    (Proposal C - hybrid/fallback)
```

By abstracting behind an interface, we can:
1. Ship with Proposal A
2. Test Proposal B in parallel
3. Use Proposal C as a fast pre-filter (lexical match before expensive embedding)
4. Swap models without touching classification logic

---

## Next Steps

1. **Prototype** Proposal A: export all-MiniLM-L6-v2 + tokenizer to ONNX, load in a .NET 8 console app, benchmark memory and latency on target hardware
2. **Benchmark quality**: Generate embeddings for a sample set of real error logs, measure clustering quality (silhouette score, purity)
3. **Test Proposal B** side-by-side: same sample set, compare grouping quality vs. Proposal A
4. **Decision gate**: If Proposal B quality >= 90% of Proposal A quality, prefer B for the memory savings. Otherwise, go with A.

---

## Sources

- [ONNX Runtime - Getting Started with C#](https://onnxruntime.ai/docs/get-started/with-csharp.html)
- [Cross-Language Embedding Generation with ONNX (yuniko.software)](https://yuniko.software/hugging-face-tokenizer-to-onnx-model/)
- [AllMiniLmL6V2Sharp NuGet](https://www.nuget.org/packages/AllMiniLmL6V2Sharp) / [GitHub](https://github.com/ksanman/AllMiniLML6v2Sharp)
- [Microsoft.ML.OnnxRuntime NuGet](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime)
- [SmartComponents.LocalEmbeddings docs](https://github.com/dotnet/smartcomponents/blob/main/docs/local-embeddings.md)
- [all-MiniLM-L6-v2 on Hugging Face](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)
- [all-MiniLM-L6-v2 Memory Requirements](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/discussions/39)
- [MongoDB LEAF Models](https://www.mongodb.com/company/blog/engineering/leaf-distillation-state-of-the-art-text-embedding-models)
- [FastText.NetWrapper NuGet](https://www.nuget.org/packages/FastText.NetWrapper/)
- [ML.NET TextFeaturizingEstimator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.transforms.text.textfeaturizingestimator)
- [Best Open-Source Embedding Models Benchmarked (supermemory.ai)](https://supermemory.ai/blog/best-open-source-embedding-models-benchmarked-and-ranked/)
- [Open-Source Embedding Models Guide (BentoML)](https://www.bentoml.com/blog/a-guide-to-open-source-embedding-models)
