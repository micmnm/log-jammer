# Embedding-Based Error Grouping Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move embedding-based similarity search into the ingestion pipeline so semantically identical log messages with different formatting get grouped together immediately, instead of relying on a delayed async merge that often misses.

**Architecture:** When a fingerprint hash lookup misses, the pipeline computes an embedding via the existing ONNX model and searches pgvector for a similar `KnownError`. If found above threshold, it groups with the existing error and creates a `FingerprintAlias` for future fast-path lookups. The `FingerprintNormalizer` also gets light improvements to clean text before embedding.

**Tech Stack:** .NET 10, EF Core + pgvector, ONNX Runtime (all-MiniLM-L6-v2), xUnit + FluentAssertions + Testcontainers

---

### Task 1: Enhance FingerprintNormalizer — strip quotes and key-value labels

**Files:**
- Modify: `src/LogJammer.Infrastructure/Pipeline/FingerprintNormalizer.cs`
- Test: `src/LogJammer.Tests/Unit/Pipeline/FingerprintNormalizerTests.cs`

**Step 1: Write the failing tests**

Add these tests to `FingerprintNormalizerTests.cs`:

```csharp
[Fact]
public void Normalize_StripsDoubleQuotes()
{
    var input = "BusMessageId: \"550e8400-e29b-41d4-a716-446655440000\"";
    var result = FingerprintNormalizer.Normalize(input);
    result.Should().NotContain("\"");
}

[Fact]
public void Normalize_StripsSingleQuotes()
{
    var input = "Error in 'UserService'";
    var result = FingerprintNormalizer.Normalize(input);
    result.Should().NotContain("'");
}

[Fact]
public void Normalize_StripsKeyValueLabels()
{
    var input = "BusMessageId: value, BusCorrelationId: value2";
    var result = FingerprintNormalizer.Normalize(input);
    result.Should().NotContain("busmessageid");
    result.Should().NotContain("buscorrelationid");
}

[Fact]
public void Normalize_StripsHttpStatusCodePrefixes()
{
    var input = "502:BadGateway:Bad Gateway:Request failed";
    var result = FingerprintNormalizer.Normalize(input);
    result.Should().NotContain("502");
    result.Should().Contain("request failed");
}

[Fact]
public void Normalize_FormattingVariants_ProduceSameOutput()
{
    var msg1 = "BusMessageId: 92c850e9-667b-4acb-921b-0a5d9c3560e5, CorrelationId: aa96e498-5632-41ea-9d66-5135f9d87ca1, Request failed with status code BadGateway(Request host is example.ngrok-free.dev)";
    var msg2 = "BusMessageId: \"92c850e9-667b-4acb-921b-0a5d9c3560e5\", BusCorrelationId: \"aa96e498-5632-41ea-9d66-5135f9d87ca1\", \"502:BadGateway:Bad Gateway:Request failed with status code BadGateway(Request host is example.ngrok-free.dev)\"";

    var result1 = FingerprintNormalizer.Normalize(msg1);
    var result2 = FingerprintNormalizer.Normalize(msg2);

    result1.Should().Be(result2);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~FingerprintNormalizerTests" --no-build 2>&1 || dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~FingerprintNormalizerTests"`
Expected: New tests FAIL (quotes and labels are not stripped yet)

**Step 3: Implement the normalizer changes**

In `FingerprintNormalizer.cs`, add three new regex steps BEFORE the existing UUID stripping:

```csharp
// Strip double and single quotes
result = QuoteRegex().Replace(result, "");

// Strip key-value label prefixes (e.g., "BusMessageId:", "CorrelationId:")
result = KeyValueLabelRegex().Replace(result, "");

// Strip HTTP status code prefixes (e.g., "502:BadGateway:Bad Gateway:")
result = HttpStatusCodePrefixRegex().Replace(result, "");
```

Add the generated regex methods:

```csharp
[GeneratedRegex(@"[""']")]
private static partial Regex QuoteRegex();

[GeneratedRegex(@"\b\w*(?:[Ii]d|[Cc]orrelation[Ii]d|[Mm]essage[Ii]d)\s*:\s*")]
private static partial Regex KeyValueLabelRegex();

[GeneratedRegex(@"\b\d{3}:[A-Za-z]+(?::[A-Za-z ]+)*:")]
private static partial Regex HttpStatusCodePrefixRegex();
```

**Important ordering:** Add quote stripping FIRST (before UUID stripping), then key-value labels, then HTTP status codes. This ensures quoted UUIDs like `"550e8400-..."` have their quotes removed before UUID regex runs.

The full `Normalize` method should be:

```csharp
public static string Normalize(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return string.Empty;

    var result = input;

    // Strip quotes (before other patterns so quoted values get unquoted first)
    result = QuoteRegex().Replace(result, "");

    // Strip key-value label prefixes (BusMessageId:, CorrelationId:, etc.)
    result = KeyValueLabelRegex().Replace(result, "");

    // Strip HTTP status code prefixes (502:BadGateway:Bad Gateway:)
    result = HttpStatusCodePrefixRegex().Replace(result, "");

    // Strip ISO timestamps (2024-01-15T10:30:45.123Z)
    result = IsoTimestampRegex().Replace(result, "");

    // Strip UUIDs
    result = UuidRegex().Replace(result, "");

    // Strip memory addresses (0x1a2b3c)
    result = MemoryAddressRegex().Replace(result, "");

    // Strip line numbers (:123, line 42)
    result = LineNumberColonRegex().Replace(result, "");
    result = LineNumberWordRegex().Replace(result, "");

    // Strip request/correlation/trace IDs (req-abc123, corr-xyz, trace-456)
    result = RequestIdRegex().Replace(result, "");

    // Collapse whitespace, lowercase, trim
    result = WhitespaceRegex().Replace(result, " ");
    result = result.Trim().ToLowerInvariant();

    return result;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~FingerprintNormalizerTests"`
Expected: ALL tests PASS (existing + new)

**Step 5: Also run the FingerprintCalculator tests to check for regressions**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~FingerprintCalculatorTests"`
Expected: ALL PASS

**Step 6: Commit**

```bash
git add src/LogJammer.Infrastructure/Pipeline/FingerprintNormalizer.cs src/LogJammer.Tests/Unit/Pipeline/FingerprintNormalizerTests.cs
git commit -m "feat: enhance normalizer with quote, label, and status code stripping"
```

---

### Task 2: Add `FindNearestByEmbeddingAsync` to `IKnownErrorRepository`

**Files:**
- Modify: `src/LogJammer.Core/Interfaces/IKnownErrorRepository.cs`
- Modify: `src/LogJammer.Infrastructure/Repositories/KnownErrorRepository.cs`
- Test: `src/LogJammer.Tests/Unit/Pipeline/SimilarityMergeTests.cs`

**Step 1: Write the failing test**

Add to `SimilarityMergeTests.cs`:

```csharp
[SkippableFact]
public async Task FindNearestByEmbeddingAsync_ReturnsMatch_WhenAboveThreshold()
{
    var target = await CreateKnownError("hash-embed-target", "Target error");

    // Store a known embedding on target
    var fakeEmbedding = new float[384];
    fakeEmbedding[0] = 1.0f; // unit vector along dim 0
    target.EmbeddingVector = new Pgvector.Vector(fakeEmbedding);
    _context.KnownErrors.Update(target);
    await _context.SaveChangesAsync();

    // Search with a very similar vector
    var queryEmbedding = new float[384];
    queryEmbedding[0] = 0.99f;
    queryEmbedding[1] = 0.01f;
    // Normalize
    var norm = (float)Math.Sqrt(queryEmbedding.Sum(v => v * v));
    for (int i = 0; i < queryEmbedding.Length; i++) queryEmbedding[i] /= norm;

    var (match, similarity) = await _repo.FindNearestByEmbeddingAsync(queryEmbedding, 0.80);

    match.Should().NotBeNull();
    match!.Id.Should().Be(target.Id);
    similarity.Should().BeGreaterThan(0.80);
}

[SkippableFact]
public async Task FindNearestByEmbeddingAsync_ReturnsNull_WhenBelowThreshold()
{
    var target = await CreateKnownError("hash-embed-far", "Target error");

    var fakeEmbedding = new float[384];
    fakeEmbedding[0] = 1.0f;
    target.EmbeddingVector = new Pgvector.Vector(fakeEmbedding);
    _context.KnownErrors.Update(target);
    await _context.SaveChangesAsync();

    // Completely different vector
    var queryEmbedding = new float[384];
    queryEmbedding[383] = 1.0f;

    var (match, similarity) = await _repo.FindNearestByEmbeddingAsync(queryEmbedding, 0.80);

    match.Should().BeNull();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~SimilarityMergeTests.FindNearest"`
Expected: FAIL (method does not exist)

**Step 3: Add interface method**

In `IKnownErrorRepository.cs`, add:

```csharp
Task<(KnownError? Match, double Similarity)> FindNearestByEmbeddingAsync(
    float[] embedding, double threshold, CancellationToken cancellationToken = default);
```

**Step 4: Implement in `KnownErrorRepository.cs`**

```csharp
public async Task<(KnownError? Match, double Similarity)> FindNearestByEmbeddingAsync(
    float[] embedding, double threshold, CancellationToken cancellationToken = default)
{
    var vector = new Pgvector.Vector(embedding);

    var nearest = await context.KnownErrors
        .Where(e => e.EmbeddingVector != null)
        .OrderBy(e => e.EmbeddingVector!.CosineDistance(vector))
        .Select(e => new { Error = e, Distance = e.EmbeddingVector!.CosineDistance(vector) })
        .Take(1)
        .FirstOrDefaultAsync(cancellationToken);

    if (nearest is null)
        return (null, 0);

    var similarity = 1.0 - nearest.Distance;
    if (similarity < threshold)
        return (null, similarity);

    return (nearest.Error, similarity);
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~SimilarityMergeTests"`
Expected: ALL PASS

**Step 6: Commit**

```bash
git add src/LogJammer.Core/Interfaces/IKnownErrorRepository.cs src/LogJammer.Infrastructure/Repositories/KnownErrorRepository.cs src/LogJammer.Tests/Unit/Pipeline/SimilarityMergeTests.cs
git commit -m "feat: add FindNearestByEmbeddingAsync to KnownErrorRepository"
```

---

### Task 3: Seed new configuration entries

**Files:**
- Modify: `src/LogJammer.Infrastructure/Data/Seeding/ClassificationConfigSeeder.cs`

**Step 1: Add the new config entries to the Defaults array**

Add two entries to the `Defaults` array in `ClassificationConfigSeeder.cs`:

```csharp
("IngestionSimilarityThreshold", "0.80", "Cosine similarity threshold for embedding-based grouping at ingestion time"),
("IngestionSimilarityEnabled", "true", "Enable/disable embedding-based similarity search during ingestion")
```

**Step 2: Verify build succeeds**

Run: `dotnet build src/LogJammer.Infrastructure`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/LogJammer.Infrastructure/Data/Seeding/ClassificationConfigSeeder.cs
git commit -m "feat: seed IngestionSimilarityThreshold and IngestionSimilarityEnabled configs"
```

---

### Task 4: Add embedding fallback to `LogIngestionPipeline`

This is the core change. The pipeline gains `IEmbeddingProvider`, `IClassificationConfigRepository`, and `LogJammerDbContext` access (it already has `LogJammerDbContext`).

**Files:**
- Modify: `src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs`
- Test: `src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs`

**Step 1: Write the failing test for embedding-based grouping**

Add to `LogIngestionPipelineTests.cs`. This test needs the ONNX model, so it follows the same pattern as `ClassificationServiceTests`:

```csharp
[SkippableFact]
public async Task ProcessEntries_SemanticallyIdenticalMessages_GroupedTogether()
{
    var dataSource = new DataSource
    {
        Name = "Test Embedding Grouping",
        AdapterType = AdapterType.KibanaProxy,
        ConnectionConfig = "{}",
        Enabled = true,
        PollIntervalSeconds = 60,
        SamplingBudget = 500
    };
    _context.DataSources.Add(dataSource);
    await _context.SaveChangesAsync();

    // Seed the config
    var configRepo = new ClassificationConfigRepository(_context);
    await configRepo.UpsertAsync("IngestionSimilarityThreshold", "0.80");
    await configRepo.UpsertAsync("IngestionSimilarityEnabled", "true");

    var pipeline = CreatePipelineWithEmbedding();

    // First entry — creates new KnownError
    var entries1 = new List<RawLogEntry>
    {
        new(DateTime.UtcNow, new Dictionary<string, object?>
        {
            ["message"] = "Request failed with status code BadGateway(Request host is example.ngrok-free.dev)",
            ["level"] = "Error"
        })
    };
    var result1 = await pipeline.ProcessEntriesAsync(dataSource, entries1, 1.0);
    Assert.Equal(1, result1.Accepted);

    // Second entry — different formatting but same semantic error
    var entries2 = new List<RawLogEntry>
    {
        new(DateTime.UtcNow, new Dictionary<string, object?>
        {
            ["message"] = "\"502:BadGateway:Bad Gateway:Request failed with status code BadGateway(Request host is example.ngrok-free.dev)\"",
            ["level"] = "Error"
        })
    };
    var result2 = await pipeline.ProcessEntriesAsync(dataSource, entries2, 1.0);

    // Should be grouped with existing, not accepted as new
    Assert.Equal(0, result2.Accepted);
    Assert.Equal(1, result2.Duplicates);

    // Only one KnownError should exist
    var knownErrors = await _context.KnownErrors
        .Where(ke => ke.DataSourceId == dataSource.Id)
        .ToListAsync();
    Assert.Single(knownErrors);

    // A FingerprintAlias should exist for the second hash
    var aliases = await _context.FingerprintAliases
        .Where(a => a.KnownErrorId == knownErrors[0].Id)
        .ToListAsync();
    Assert.Single(aliases);
}
```

The test also needs a new helper `CreatePipelineWithEmbedding()`:

```csharp
private LogIngestionPipeline CreatePipelineWithEmbedding()
{
    var schemaMapper = new SchemaMapper();
    var fingerprintCalculator = new FingerprintCalculator();
    var knownErrorRepo = new KnownErrorRepository(_context);
    var occurrenceRepo = new ErrorOccurrenceRepository(_context);
    var configRepo = new ClassificationConfigRepository(_context);

    var modelDir = Path.Combine(Path.GetTempPath(), "logjammer-test-models", "all-MiniLM-L6-v2");
    var downloader = new ModelDownloader(modelDir, Microsoft.Extensions.Logging.Abstractions.NullLogger<ModelDownloader>.Instance);
    _embeddingProvider = new OnnxEmbeddingProvider(downloader, Microsoft.Extensions.Logging.Abstractions.NullLogger<OnnxEmbeddingProvider>.Instance);

    var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogIngestionPipeline>.Instance;
    return new LogIngestionPipeline(schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo, _context, _embeddingProvider, configRepo, logger);
}
```

You'll also need to add a class-level field `private OnnxEmbeddingProvider? _embeddingProvider;` and update `DisposeAsync` to dispose it. Also update the existing `CreatePipeline()` helper to pass `null` for the new optional parameters (or a no-op embedding provider).

**Important:** Add these `using` directives at the top of the test file:
```csharp
using LogJammer.Infrastructure.ML;
using LogJammer.Infrastructure.Repositories;
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~LogIngestionPipelineTests.ProcessEntries_SemanticallyIdenticalMessages"`
Expected: FAIL (constructor signature mismatch)

**Step 3: Modify `LogIngestionPipeline` to accept new dependencies and add embedding fallback**

Update the constructor to accept optional `IEmbeddingProvider?` and `IClassificationConfigRepository?`:

```csharp
public class LogIngestionPipeline(
    ISchemaMapper schemaMapper,
    IFingerprintCalculator fingerprintCalculator,
    IKnownErrorRepository knownErrorRepo,
    IErrorOccurrenceRepository occurrenceRepo,
    LogJammerDbContext dbContext,
    IEmbeddingProvider? embeddingProvider,
    IClassificationConfigRepository? configRepo,
    ILogger<LogIngestionPipeline> logger) : ILogIngestionPipeline
```

Then modify the body of `ProcessEntriesAsync`. After the fingerprint hash miss and alias miss (`knownError` is still `null`), add the embedding fallback:

```csharp
// Embedding-based similarity fallback
if (knownError is null && embeddingProvider is not null && configRepo is not null)
{
    knownError = await TryFindByEmbeddingSimilarityAsync(mapped, cancellationToken);
}
```

Add the private helper method:

```csharp
private async Task<KnownError?> TryFindByEmbeddingSimilarityAsync(
    MappedLogEntry mapped, CancellationToken ct)
{
    // Check feature flag
    var enabledConfig = await configRepo!.GetAsync("IngestionSimilarityEnabled", ct);
    if (enabledConfig is not null && !bool.TryParse(enabledConfig.Value, out var enabled) || enabledConfig is not null && bool.TryParse(enabledConfig.Value, out enabled) && !enabled)
        return null;

    // Load threshold
    var thresholdConfig = await configRepo.GetAsync("IngestionSimilarityThreshold", ct);
    var threshold = 0.80;
    if (thresholdConfig is not null && double.TryParse(thresholdConfig.Value, out var t))
        threshold = t;

    // Normalize text for better embedding input
    var text = FingerprintNormalizer.Normalize(mapped.Message);
    if (!string.IsNullOrWhiteSpace(mapped.StackTrace))
        text += " " + FingerprintNormalizer.Normalize(mapped.StackTrace);

    if (string.IsNullOrWhiteSpace(text))
        return null;

    // Compute embedding
    var embedding = await embeddingProvider!.GenerateEmbeddingAsync(text, ct);

    // Search for nearest neighbor
    var (match, similarity) = await knownErrorRepo.FindNearestByEmbeddingAsync(embedding, threshold, ct);

    if (match is null)
    {
        // No match — store the embedding for future lookups so the new KnownError
        // (created by the caller) can be matched against later.
        // We'll store it after the KnownError is created (see below).
        _pendingEmbedding = new Pgvector.Vector(embedding);
        return null;
    }

    logger.LogInformation(
        "Embedding similarity match: grouped with KnownError {TargetId} (similarity={Similarity:F3})",
        match.Id, similarity);

    return match;
}
```

Add a class-level field to hold the pending embedding:

```csharp
private Pgvector.Vector? _pendingEmbedding;
```

Then in the main loop body, after creating a new `KnownError` (the `knownError is null` branch that calls `knownErrorRepo.AddAsync`), store the pending embedding:

```csharp
if (knownError is null)
{
    knownError = await knownErrorRepo.AddAsync(new KnownError
    {
        FingerprintHash = fingerprint,
        RepresentativeMessage = mapped.Message,
        RepresentativeStackTrace = mapped.StackTrace,
        Severity = mapped.Severity ?? ErrorSeverity.Warning,
        Status = ErrorStatus.Active,
        FirstSeen = mapped.Timestamp,
        LastSeen = mapped.Timestamp,
        TotalOccurrences = 1,
        DataSourceId = dataSource.Id,
        EmbeddingVector = _pendingEmbedding  // Store pre-computed embedding
    }, cancellationToken);

    _pendingEmbedding = null;

    dbContext.ClassificationQueue.Add(new ClassificationQueueItem
    {
        KnownErrorId = knownError.Id
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    accepted++;
}
```

When the embedding fallback DID find a match (knownError is not null from embedding), we need to create a `FingerprintAlias`:

After the embedding fallback call, before the `if (knownError is null)` branch, add an `else if` that detects the embedding-match case and creates the alias:

The full restructured flow inside the foreach loop becomes:

```csharp
var mapped = schemaMapper.Map(entry, dataSource.SchemaMapping);
var fingerprint = fingerprintCalculator.ComputeFingerprint(mapped, fingerprintConfigs);

var knownError = await knownErrorRepo.GetByFingerprintHashAsync(fingerprint, cancellationToken);
knownError ??= await knownErrorRepo.GetByFingerprintAliasAsync(fingerprint, cancellationToken);

var matchedByEmbedding = false;

// Embedding-based similarity fallback
if (knownError is null && embeddingProvider is not null && configRepo is not null)
{
    knownError = await TryFindByEmbeddingSimilarityAsync(mapped, cancellationToken);
    matchedByEmbedding = knownError is not null;
}

if (knownError is null)
{
    // Brand new error
    knownError = await knownErrorRepo.AddAsync(new KnownError
    {
        FingerprintHash = fingerprint,
        RepresentativeMessage = mapped.Message,
        RepresentativeStackTrace = mapped.StackTrace,
        Severity = mapped.Severity ?? ErrorSeverity.Warning,
        Status = ErrorStatus.Active,
        FirstSeen = mapped.Timestamp,
        LastSeen = mapped.Timestamp,
        TotalOccurrences = 1,
        DataSourceId = dataSource.Id,
        EmbeddingVector = _pendingEmbedding
    }, cancellationToken);

    _pendingEmbedding = null;

    dbContext.ClassificationQueue.Add(new ClassificationQueueItem
    {
        KnownErrorId = knownError.Id
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    accepted++;
}
else
{
    // Existing error — increment
    knownError.LastSeen = mapped.Timestamp > knownError.LastSeen ? mapped.Timestamp : knownError.LastSeen;
    knownError.TotalOccurrences++;
    await knownErrorRepo.UpdateAsync(knownError, cancellationToken);

    // Create alias if matched by embedding (so future lookups are fast)
    if (matchedByEmbedding)
    {
        dbContext.FingerprintAliases.Add(new FingerprintAlias
        {
            FingerprintHash = fingerprint,
            KnownErrorId = knownError.Id
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    duplicates++;
}

await occurrenceRepo.UpsertWindowAsync(
    knownError.Id, mapped.Timestamp, mapped.Timestamp.AddMinutes(5),
    sampleRatio, cancellationToken);
```

**Step 4: Update the existing `CreatePipeline()` helper in tests**

The existing `CreatePipeline()` needs to pass `null` for the new optional parameters:

```csharp
private LogIngestionPipeline CreatePipeline()
{
    var schemaMapper = new SchemaMapper();
    var fingerprintCalculator = new FingerprintCalculator();
    var knownErrorRepo = new KnownErrorRepository(_context);
    var occurrenceRepo = new ErrorOccurrenceRepository(_context);
    var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogIngestionPipeline>.Instance;
    return new LogIngestionPipeline(schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo, _context, null, null, logger);
}
```

**Step 5: Run all pipeline tests**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~LogIngestionPipelineTests"`
Expected: ALL PASS (existing tests still work with null embedding provider; new test passes)

**Step 6: Commit**

```bash
git add src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs
git commit -m "feat: add embedding-based similarity fallback to ingestion pipeline"
```

---

### Task 5: Update DI registration

**Files:**
- Modify: `src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs`

**Step 1: Update the `LogIngestionPipeline` registration**

The `LogIngestionPipeline` now takes `IEmbeddingProvider?` and `IClassificationConfigRepository?`. Since both are already registered in the DI container (`IEmbeddingProvider` as singleton, `IClassificationConfigRepository` as scoped), the DI container will resolve them automatically. No explicit change is needed IF the constructor parameters are non-nullable interfaces.

However, we made them nullable (`IEmbeddingProvider?`) for test flexibility. DI won't resolve nullable parameters automatically. We have two options:

**Option A (recommended):** Make the constructor parameters non-nullable and update the test helper to provide implementations:

Change the pipeline constructor to:
```csharp
public class LogIngestionPipeline(
    ISchemaMapper schemaMapper,
    IFingerprintCalculator fingerprintCalculator,
    IKnownErrorRepository knownErrorRepo,
    IErrorOccurrenceRepository occurrenceRepo,
    LogJammerDbContext dbContext,
    IEmbeddingProvider embeddingProvider,
    IClassificationConfigRepository configRepo,
    ILogger<LogIngestionPipeline> logger) : ILogIngestionPipeline
```

And in the pipeline, remove the null checks (they'll always be provided). Update the old `CreatePipeline()` test helper to use NSubstitute mocks:

```csharp
private LogIngestionPipeline CreatePipeline()
{
    var schemaMapper = new SchemaMapper();
    var fingerprintCalculator = new FingerprintCalculator();
    var knownErrorRepo = new KnownErrorRepository(_context);
    var occurrenceRepo = new ErrorOccurrenceRepository(_context);
    var embeddingProvider = NSubstitute.Substitute.For<IEmbeddingProvider>();
    var configRepo = new ClassificationConfigRepository(_context);
    var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogIngestionPipeline>.Instance;
    return new LogIngestionPipeline(schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo, _context, embeddingProvider, configRepo, logger);
}
```

When `IngestionSimilarityEnabled` is not seeded, the feature is treated as disabled (no config = skip), so existing tests continue to work without hitting the ONNX model.

Actually, wait — the seeder defaults `IngestionSimilarityEnabled` to `"true"`. For existing tests that use `CreatePipeline()` (without the real embedding provider), we need the mock `IEmbeddingProvider` to not blow up. Since NSubstitute returns default values (null/0 for unmatched calls), and the pipeline calls `GenerateEmbeddingAsync`, we should configure the mock to return an empty float array when called — OR ensure the feature flag check prevents the call.

Simplest approach: in `CreatePipeline()`, don't seed `IngestionSimilarityEnabled`, so it remains absent from the DB. In `TryFindByEmbeddingSimilarityAsync`, treat missing config as **disabled**:

```csharp
var enabledConfig = await configRepo!.GetAsync("IngestionSimilarityEnabled", ct);
if (enabledConfig is null || !bool.TryParse(enabledConfig.Value, out var enabled) || !enabled)
    return null;
```

This means: if the config doesn't exist, skip embedding. It's only active when explicitly seeded. The `ClassificationConfigSeeder` seeds it on startup, but tests that use `CreatePipeline()` (without running the seeder) won't have it.

**Step 2: Verify DI resolves correctly**

Run: `dotnet build src/LogJammer.Api`
Expected: Build succeeded

**Step 3: Run ALL tests**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~LogIngestionPipelineTests"`
Expected: ALL PASS

**Step 4: Commit**

```bash
git add src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs
git commit -m "feat: wire embedding provider into pipeline via DI"
```

---

### Task 6: Full integration test with real ONNX model

**Files:**
- Test: `src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs`

**Step 1: Verify the end-to-end scenario test from Task 4 passes with Docker + ONNX**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~LogIngestionPipelineTests.ProcessEntries_SemanticallyIdenticalMessages" -v n`
Expected: PASS — the two formatting-variant messages are grouped into one `KnownError` with a `FingerprintAlias`

**Step 2: Add a negative test — dissimilar messages stay separate**

```csharp
[SkippableFact]
public async Task ProcessEntries_DissimilarMessages_StaySeparate()
{
    var dataSource = new DataSource
    {
        Name = "Test Dissimilar",
        AdapterType = AdapterType.KibanaProxy,
        ConnectionConfig = "{}",
        Enabled = true,
        PollIntervalSeconds = 60,
        SamplingBudget = 500
    };
    _context.DataSources.Add(dataSource);
    await _context.SaveChangesAsync();

    var configRepo = new ClassificationConfigRepository(_context);
    await configRepo.UpsertAsync("IngestionSimilarityThreshold", "0.80");
    await configRepo.UpsertAsync("IngestionSimilarityEnabled", "true");

    var pipeline = CreatePipelineWithEmbedding();

    var entries1 = new List<RawLogEntry>
    {
        new(DateTime.UtcNow, new Dictionary<string, object?>
        {
            ["message"] = "Request failed with status code BadGateway(Request host is example.ngrok-free.dev)",
            ["level"] = "Error"
        })
    };
    await pipeline.ProcessEntriesAsync(dataSource, entries1, 1.0);

    var entries2 = new List<RawLogEntry>
    {
        new(DateTime.UtcNow, new Dictionary<string, object?>
        {
            ["message"] = "Redis connection timeout after 30 seconds to cache cluster",
            ["level"] = "Error"
        })
    };
    var result2 = await pipeline.ProcessEntriesAsync(dataSource, entries2, 1.0);

    // Should create a new KnownError (not grouped)
    Assert.Equal(1, result2.Accepted);

    var knownErrors = await _context.KnownErrors
        .Where(ke => ke.DataSourceId == dataSource.Id)
        .ToListAsync();
    Assert.Equal(2, knownErrors.Count);
}
```

**Step 3: Run test**

Run: `dotnet test src/LogJammer.Tests --filter "FullyQualifiedName~LogIngestionPipelineTests.ProcessEntries_DissimilarMessages"`
Expected: PASS

**Step 4: Run the full test suite to check for regressions**

Run: `dotnet test src/LogJammer.Tests`
Expected: ALL PASS (some may be skipped if Docker unavailable — that's expected)

**Step 5: Commit**

```bash
git add src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs
git commit -m "test: add integration tests for embedding-based grouping"
```

---

### Task 7: Final verification and cleanup

**Step 1: Build the full solution**

Run: `dotnet build`
Expected: Build succeeded, 0 warnings related to our changes

**Step 2: Run all tests one final time**

Run: `dotnet test src/LogJammer.Tests -v n`
Expected: ALL PASS

**Step 3: Verify the Docker build works**

Run: `docker compose build api`
Expected: Build succeeds

**Step 4: Final commit if any cleanup was needed**

Only if there were issues in the previous steps that required fixes.
