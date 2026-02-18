# Classification Pipeline Upgrade — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Upgrade the ML classification pipeline with feature extraction, composite similarity scoring, queue clustering, adaptive thresholds, and a SampleLog mock ES server.

**Architecture:** The classification pipeline gains three new layers: (1) ErrorFeatureExtractor parses structured features from log messages, (2) CompositeScorer replaces raw cosine similarity with a weighted multi-signal score, (3) ClassificationClusterService groups similar pending queue items into clusters. The frontend shows clusters instead of flat items, enabling bulk approve/reject. A new mock ES HTTP server in SampleLog enables end-to-end testing.

**Tech Stack:** .NET 10 / C# 13 (backend), EF Core 10 + PostgreSQL (persistence), React 19 + MUI 7 + TanStack Query 5 (frontend), xUnit + Testcontainers (tests), ASP.NET Minimal API (SampleLog mock server)

---

## Task 1: Database Schema Changes

**Files:**
- Modify: `src/LogJammer.Core/Entities/KnownError.cs`
- Modify: `src/LogJammer.Core/Entities/ClassificationQueueItem.cs`
- Create: `src/LogJammer.Core/Entities/ClassificationDecision.cs`
- Modify: `src/LogJammer.Infrastructure/Data/Configurations/KnownErrorConfiguration.cs`
- Modify: `src/LogJammer.Infrastructure/Data/Configurations/ClassificationQueueItemConfiguration.cs`
- Create: `src/LogJammer.Infrastructure/Data/Configurations/ClassificationDecisionConfiguration.cs`
- Modify: `src/LogJammer.Infrastructure/Data/LogJammerDbContext.cs`
- Modify: `src/LogJammer.Infrastructure/Data/Seeding/` (seed new config entries)
- Migration auto-generated

**Step 1: Add `ExtractedFeatures` to KnownError entity**

In `src/LogJammer.Core/Entities/KnownError.cs`, add after `OccurrenceWindows`:

```csharp
public string? ExtractedFeatures { get; set; } // JSON
```

In `src/LogJammer.Infrastructure/Data/Configurations/KnownErrorConfiguration.cs`, add after the `OccurrenceWindows` line:

```csharp
builder.Property(e => e.ExtractedFeatures).HasColumnName("extracted_features").HasColumnType("jsonb");
```

**Step 2: Add `ClusterId` to ClassificationQueueItem entity**

In `src/LogJammer.Core/Entities/ClassificationQueueItem.cs`, add after `ReviewedAt`:

```csharp
public Guid? ClusterId { get; set; }
```

In `src/LogJammer.Infrastructure/Data/Configurations/ClassificationQueueItemConfiguration.cs`, add:

```csharp
builder.Property(e => e.ClusterId).HasColumnName("cluster_id");
builder.HasIndex(e => e.ClusterId);
```

**Step 3: Create ClassificationDecision entity**

Create `src/LogJammer.Core/Entities/ClassificationDecision.cs`:

```csharp
namespace LogJammer.Core.Entities;

public class ClassificationDecision
{
    public Guid Id { get; set; }
    public Guid KnownErrorId { get; set; }
    public Guid? ClusterId { get; set; }
    public double SimilarityScore { get; set; }
    public required string Decision { get; set; } // "approve" or "reject"
    public DateTime CreatedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
}
```

Create `src/LogJammer.Infrastructure/Data/Configurations/ClassificationDecisionConfiguration.cs`:

```csharp
using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class ClassificationDecisionConfiguration : IEntityTypeConfiguration<ClassificationDecision>
{
    public void Configure(EntityTypeBuilder<ClassificationDecision> builder)
    {
        builder.ToTable("classification_decisions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.ClusterId).HasColumnName("cluster_id");
        builder.Property(e => e.SimilarityScore).HasColumnName("similarity_score");
        builder.Property(e => e.Decision).HasColumnName("decision").HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.KnownError)
            .WithMany()
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CreatedAt);
    }
}
```

**Step 4: Add DbSet to context**

In `src/LogJammer.Infrastructure/Data/LogJammerDbContext.cs`, add:

```csharp
public DbSet<ClassificationDecision> ClassificationDecisions => Set<ClassificationDecision>();
```

**Step 5: Seed new configuration entries**

In the config seeding logic (wherever `SimilarityThreshold` etc. are seeded), add:

```csharp
await configRepo.UpsertAsync("EmbeddingWeight", "0.50", "Weight of embedding similarity in composite score");
await configRepo.UpsertAsync("TemplateWeight", "0.20", "Weight of message template similarity");
await configRepo.UpsertAsync("StructuralWeight", "0.20", "Weight of structural feature matches");
await configRepo.UpsertAsync("MetadataWeight", "0.10", "Weight of logger/frame overlap");
await configRepo.UpsertAsync("ClusteringEnabled", "true", "Pre-cluster similar queue items");
await configRepo.UpsertAsync("ClusteringThreshold", "0.70", "Minimum composite score to join a cluster");
await configRepo.UpsertAsync("AdaptiveThresholdEnabled", "false", "Show adaptive threshold suggestions");
```

**Step 6: Generate and apply migration**

Run:
```bash
cd src && dotnet ef migrations add ClassificationPipelineUpgrade --project LogJammer.Infrastructure --startup-project LogJammer.Api
```

**Step 7: Verify build**

Run: `dotnet build src/LogJammer.slnx`
Expected: Build succeeds with no errors.

**Step 8: Commit**

```bash
git add src/LogJammer.Core/Entities/ src/LogJammer.Infrastructure/Data/
git commit -m "feat: add schema for classification pipeline upgrade

Add ExtractedFeatures jsonb to KnownError, ClusterId to ClassificationQueueItem,
ClassificationDecision entity, and seed new scoring weight configs."
```

---

## Task 2: Feature Extraction

**Files:**
- Create: `src/LogJammer.Core/Models/ExtractedFeatures.cs`
- Create: `src/LogJammer.Infrastructure/ML/ErrorFeatureExtractor.cs`
- Create: `src/LogJammer.Tests/Unit/ML/ErrorFeatureExtractorTests.cs`

**Step 1: Create the ExtractedFeatures model**

Create `src/LogJammer.Core/Models/ExtractedFeatures.cs`:

```csharp
namespace LogJammer.Core.Models;

public record ExtractedFeatures(
    string? Level,
    string? Application,
    string? ExceptionType,
    string? Logger,
    string? MessageTemplate,
    IReadOnlyList<string> TopFrames);
```

**Step 2: Write failing tests for ErrorFeatureExtractor**

Create `src/LogJammer.Tests/Unit/ML/ErrorFeatureExtractorTests.cs`:

```csharp
using FluentAssertions;
using LogJammer.Infrastructure.ML;

namespace LogJammer.Tests.Unit.ML;

public class ErrorFeatureExtractorTests
{
    private readonly ErrorFeatureExtractor _extractor = new();

    [Fact]
    public void Extract_WithExceptionType_ExtractsFromMessage()
    {
        var features = _extractor.Extract(
            message: "System.NullReferenceException: Object reference not set to an instance of an object",
            stackTrace: null,
            schemaFields: null);

        features.ExceptionType.Should().Be("NullReferenceException");
    }

    [Fact]
    public void Extract_WithStackTrace_ExtractsTopFrames()
    {
        var stackTrace = """
            at MyApp.Services.OrderService.Process(Order order) in OrderService.cs:line 45
            at MyApp.Controllers.OrderController.Submit(OrderRequest req) in OrderController.cs:line 23
            at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start()
            at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.Execute()
            """;

        var features = _extractor.Extract(
            message: "Request failed",
            stackTrace: stackTrace,
            schemaFields: null);

        features.TopFrames.Should().HaveCount(2);
        features.TopFrames[0].Should().Contain("OrderService.Process");
        features.TopFrames[1].Should().Contain("OrderController.Submit");
    }

    [Fact]
    public void Extract_WithSchemaFields_UsesDirectValues()
    {
        var fields = new Dictionary<string, string>
        {
            ["Application"] = "PaymentService",
            ["SourceContext"] = "Checkout.OrderProcessor",
            ["Level"] = "Error"
        };

        var features = _extractor.Extract(
            message: "Request failed",
            stackTrace: null,
            schemaFields: fields);

        features.Application.Should().Be("PaymentService");
        features.Logger.Should().Be("Checkout.OrderProcessor");
        features.Level.Should().Be("Error");
    }

    [Fact]
    public void Extract_NormalizesMessageTemplate()
    {
        var features = _extractor.Extract(
            message: "Request POST /api/orders/12345 completed with 500 in 234ms from 192.168.1.100",
            stackTrace: null,
            schemaFields: null);

        // Numbers, IPs, UUIDs should be normalized
        features.MessageTemplate.Should().NotContain("12345");
        features.MessageTemplate.Should().NotContain("192.168.1.100");
        features.MessageTemplate.Should().Contain("/api/orders/");
    }

    [Fact]
    public void Extract_WithHttpErrorType_ExtractsFromPattern()
    {
        var features = _extractor.Extract(
            message: "HTTP 500 Internal Server Error at POST /api/checkout",
            stackTrace: null,
            schemaFields: null);

        features.ExceptionType.Should().BeNull(); // No .NET exception
        features.MessageTemplate.Should().Contain("HTTP");
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~ErrorFeatureExtractorTests" -v n`
Expected: FAIL — `ErrorFeatureExtractor` class doesn't exist.

**Step 4: Implement ErrorFeatureExtractor**

Create `src/LogJammer.Infrastructure/ML/ErrorFeatureExtractor.cs`:

```csharp
using System.Text.RegularExpressions;
using LogJammer.Core.Models;

namespace LogJammer.Infrastructure.ML;

public partial class ErrorFeatureExtractor
{
    // Framework namespaces to skip in stack traces
    private static readonly HashSet<string> FrameworkPrefixes =
    [
        "System.", "Microsoft.", "Npgsql.", "Serilog.", "Elastic.",
        "Newtonsoft.", "Castle.", "DynamicProxyGen", "lambda_method"
    ];

    public ExtractedFeatures Extract(
        string message,
        string? stackTrace,
        Dictionary<string, string>? schemaFields)
    {
        var level = schemaFields?.GetValueOrDefault("Level");
        var application = schemaFields?.GetValueOrDefault("Application")
            ?? schemaFields?.GetValueOrDefault("ServiceName");
        var logger = schemaFields?.GetValueOrDefault("SourceContext")
            ?? schemaFields?.GetValueOrDefault("LoggerName");
        var exceptionType = schemaFields?.GetValueOrDefault("ExceptionType")
            ?? ExtractExceptionType(message);
        var messageTemplate = NormalizeMessage(message);
        var topFrames = ParseTopFrames(stackTrace, maxFrames: 3);

        return new ExtractedFeatures(
            Level: level,
            Application: application,
            ExceptionType: exceptionType,
            Logger: logger,
            MessageTemplate: messageTemplate,
            TopFrames: topFrames);
    }

    private static string? ExtractExceptionType(string message)
    {
        var match = ExceptionTypeRegex().Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string NormalizeMessage(string message)
    {
        // Strip UUIDs
        var result = UuidRegex().Replace(message, "<UUID>");
        // Strip IP addresses
        result = IpRegex().Replace(result, "<IP>");
        // Strip pure numbers (but preserve path segments like /api/orders)
        result = StandaloneNumberRegex().Replace(result, "<N>");
        // Strip timestamps
        result = TimestampRegex().Replace(result, "<TS>");
        return result;
    }

    private static IReadOnlyList<string> ParseTopFrames(string? stackTrace, int maxFrames)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return [];

        var frames = new List<string>();
        foreach (var line in stackTrace.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = StackFrameRegex().Match(line);
            if (!match.Success) continue;

            var fullMethod = match.Groups[1].Value;
            if (FrameworkPrefixes.Any(fullMethod.StartsWith)) continue;

            // Simplify to Class.Method
            var parts = fullMethod.Split('(')[0];
            var segments = parts.Split('.');
            var simplified = segments.Length >= 2
                ? $"{segments[^2]}.{segments[^1]}"
                : parts;

            frames.Add(simplified);
            if (frames.Count >= maxFrames) break;
        }

        return frames;
    }

    [GeneratedRegex(@"\b(\w+(?:Exception|Error))\b")]
    private static partial Regex ExceptionTypeRegex();

    [GeneratedRegex(@"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase)]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b")]
    private static partial Regex IpRegex();

    [GeneratedRegex(@"(?<=\s|^)\d+(?=\s|$|ms|s\b)")]
    private static partial Regex StandaloneNumberRegex();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"^\s*at\s+(.+)")]
    private static partial Regex StackFrameRegex();
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~ErrorFeatureExtractorTests" -v n`
Expected: All 5 tests PASS.

**Step 6: Commit**

```bash
git add src/LogJammer.Core/Models/ExtractedFeatures.cs src/LogJammer.Infrastructure/ML/ErrorFeatureExtractor.cs src/LogJammer.Tests/Unit/ML/ErrorFeatureExtractorTests.cs
git commit -m "feat: add ErrorFeatureExtractor for structured log parsing

Extracts exception type, application, logger, top stack frames, and
normalized message template from log messages and schema-mapped fields."
```

---

## Task 3: Composite Similarity Scoring

**Files:**
- Create: `src/LogJammer.Infrastructure/ML/CompositeScorer.cs`
- Create: `src/LogJammer.Tests/Unit/ML/CompositeScorerTests.cs`

**Step 1: Write failing tests**

Create `src/LogJammer.Tests/Unit/ML/CompositeScorerTests.cs`:

```csharp
using FluentAssertions;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.ML;

namespace LogJammer.Tests.Unit.ML;

public class CompositeScorerTests
{
    [Fact]
    public void Score_IdenticalFeatures_ReturnsOne()
    {
        var features = new ExtractedFeatures(
            Level: "Error",
            Application: "OrderService",
            ExceptionType: "NullReferenceException",
            Logger: "OrderProcessor",
            MessageTemplate: "Request failed at <N>",
            TopFrames: ["OrderService.Process", "OrderController.Submit"]);

        var weights = new ScoringWeights(0.5, 0.2, 0.2, 0.1);
        var score = CompositeScorer.Score(features, features, 1.0, weights);

        score.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Score_DifferentEverything_ReturnsLow()
    {
        var a = new ExtractedFeatures("Error", "OrderService", "NullReferenceException", "OrderProcessor",
            "Request POST failed", ["OrderService.Process"]);
        var b = new ExtractedFeatures("Warning", "AuthService", "TimeoutException", "LoginHandler",
            "Connection timed out", ["AuthService.Login"]);

        var weights = new ScoringWeights(0.5, 0.2, 0.2, 0.1);
        var score = CompositeScorer.Score(a, b, 0.3, weights);

        score.Should().BeLessThan(0.4);
    }

    [Fact]
    public void Score_SameExceptionType_BoostsStructural()
    {
        var a = new ExtractedFeatures("Error", "OrderService", "NullReferenceException", "ProcessorA",
            "Object reference at line 45", ["ProcessorA.Run"]);
        var b = new ExtractedFeatures("Error", "OrderService", "NullReferenceException", "ProcessorB",
            "Object reference at line 99", ["ProcessorB.Run"]);

        var weights = new ScoringWeights(0.5, 0.2, 0.2, 0.1);
        var score = CompositeScorer.Score(a, b, 0.6, weights);

        // Embedding only 0.6, but structural match on exception+app+level should boost
        score.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void StructuralMatch_AllFieldsMatch_ReturnsOne()
    {
        var a = new ExtractedFeatures("Error", "OrderService", "NullRef", "Logger", "msg", []);
        var b = new ExtractedFeatures("Error", "OrderService", "NullRef", "Logger", "msg", []);

        var result = CompositeScorer.StructuralMatch(a, b);
        result.Should().Be(1.0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~CompositeScorerTests" -v n`
Expected: FAIL — `CompositeScorer` class doesn't exist.

**Step 3: Implement CompositeScorer**

Create `src/LogJammer.Infrastructure/ML/CompositeScorer.cs`:

```csharp
using LogJammer.Core.Models;

namespace LogJammer.Infrastructure.ML;

public record ScoringWeights(double Embedding, double Template, double Structural, double Metadata);

public static class CompositeScorer
{
    public static readonly ScoringWeights DefaultWeights = new(0.50, 0.20, 0.20, 0.10);

    public static double Score(
        ExtractedFeatures a,
        ExtractedFeatures b,
        double embeddingSimilarity,
        ScoringWeights weights)
    {
        var templateSim = TemplateSimilarity(a.MessageTemplate, b.MessageTemplate);
        var structuralSim = StructuralMatch(a, b);
        var metadataSim = MetadataOverlap(a, b);

        return weights.Embedding * embeddingSimilarity
             + weights.Template * templateSim
             + weights.Structural * structuralSim
             + weights.Metadata * metadataSim;
    }

    public static double StructuralMatch(ExtractedFeatures a, ExtractedFeatures b)
    {
        double score = 0;
        // Exception type match is strongest signal (0.4)
        if (!string.IsNullOrEmpty(a.ExceptionType) && a.ExceptionType == b.ExceptionType)
            score += 0.4;
        // Same application (0.3)
        if (!string.IsNullOrEmpty(a.Application) && a.Application == b.Application)
            score += 0.3;
        // Same level (0.1)
        if (!string.IsNullOrEmpty(a.Level) && a.Level == b.Level)
            score += 0.1;
        // Same logger (0.2)
        if (!string.IsNullOrEmpty(a.Logger) && a.Logger == b.Logger)
            score += 0.2;

        return Math.Min(score, 1.0);
    }

    public static double TemplateSimilarity(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;
        if (a == b) return 1.0;

        // Normalized Levenshtein distance
        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    public static double MetadataOverlap(ExtractedFeatures a, ExtractedFeatures b)
    {
        if (a.TopFrames.Count == 0 && b.TopFrames.Count == 0)
            return 0;
        if (a.TopFrames.Count == 0 || b.TopFrames.Count == 0)
            return 0;

        // Jaccard similarity of top frames
        var setA = new HashSet<string>(a.TopFrames);
        var setB = new HashSet<string>(b.TopFrames);
        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            var cost = s[i - 1] == t[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }

        return d[n, m];
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~CompositeScorerTests" -v n`
Expected: All 4 tests PASS.

**Step 5: Commit**

```bash
git add src/LogJammer.Infrastructure/ML/CompositeScorer.cs src/LogJammer.Tests/Unit/ML/CompositeScorerTests.cs
git commit -m "feat: add CompositeScorer for multi-signal similarity

Combines embedding similarity, template similarity, structural feature
matching (exception type, app, level, logger), and top-frame overlap."
```

---

## Task 4: Update ClassificationService with Feature Extraction + Composite Scoring

**Files:**
- Modify: `src/LogJammer.Infrastructure/ML/ClassificationService.cs`
- Modify: `src/LogJammer.Core/Interfaces/IClassificationService.cs`
- Modify: `src/LogJammer.Tests/Unit/ML/ClassificationQueueMergeTests.cs`

**Step 1: Add ExtractAndStoreFeatures to IClassificationService**

In `src/LogJammer.Core/Interfaces/IClassificationService.cs`, add:

```csharp
Task ExtractAndStoreFeaturesAsync(KnownError error, Dictionary<string, string>? schemaFields = null, CancellationToken ct = default);
```

**Step 2: Update ClassificationService constructor to include new dependencies**

In `src/LogJammer.Infrastructure/ML/ClassificationService.cs`:

- Add `ErrorFeatureExtractor` as a dependency (can be `new`'d inline since it's stateless, or injected)
- Update `EnsureEmbeddingAsync` to compose structured text from extracted features
- Update `ClassifyAsync` to use `CompositeScorer` instead of raw cosine similarity
- Add `ExtractAndStoreFeaturesAsync` implementation

Key changes:

```csharp
// In EnsureEmbeddingAsync, replace simple text concatenation with structured text:
private static string ComposeEmbeddingText(KnownError error, ExtractedFeatures? features)
{
    var parts = new List<string>();
    if (features?.Application is not null) parts.Add($"[{features.Application}]");
    if (features?.ExceptionType is not null) parts.Add($"[{features.ExceptionType}]");
    if (features?.Level is not null) parts.Add($"[{features.Level}]");
    parts.Add(error.RepresentativeMessage);
    if (features?.TopFrames.Count > 0) parts.Add(string.Join(" ", features.TopFrames));
    else if (!string.IsNullOrWhiteSpace(error.RepresentativeStackTrace))
        parts.Add(error.RepresentativeStackTrace);
    return string.Join(" ", parts);
}

// In ClassifyAsync, after getting neighbors, compute composite score:
// 1. Parse ExtractedFeatures from both error and neighbor
// 2. Use CompositeScorer.Score() instead of raw (1.0 - distance)
// 3. Load weights from ClassificationConfig
```

**Step 3: Add ExtractAndStoreFeaturesAsync**

```csharp
public async Task ExtractAndStoreFeaturesAsync(KnownError error, Dictionary<string, string>? schemaFields = null, CancellationToken ct = default)
{
    if (error.ExtractedFeatures is not null)
        return;

    var extractor = new ErrorFeatureExtractor();
    var features = extractor.Extract(error.RepresentativeMessage, error.RepresentativeStackTrace, schemaFields);
    error.ExtractedFeatures = JsonSerializer.Serialize(features);
    context.KnownErrors.Update(error);
    await context.SaveChangesAsync(ct);
}
```

**Step 4: Update existing tests**

The `ClassificationQueueMergeTests` constructor for `ClassificationService` won't change (it takes the same parameters). But test assertions about similarity scores may shift because of composite scoring. Update `ClassifyAsync_SimilarHttpLogMessages_ShouldReturnMatchedGroupId` to check that it still works with the new scoring.

**Step 5: Run all classification tests**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~Classification" -v n`
Expected: All tests PASS.

**Step 6: Commit**

```bash
git add src/LogJammer.Core/Interfaces/IClassificationService.cs src/LogJammer.Infrastructure/ML/ClassificationService.cs src/LogJammer.Tests/
git commit -m "feat: integrate feature extraction and composite scoring into ClassificationService

ClassifyAsync now uses CompositeScorer with configurable weights instead
of raw cosine distance. EnsureEmbeddingAsync composes structured text
from extracted features for better semantic embeddings."
```

---

## Task 5: Queue Clustering Logic

**Files:**
- Create: `src/LogJammer.Infrastructure/ML/ClassificationClusterService.cs`
- Create: `src/LogJammer.Core/Interfaces/IClassificationClusterService.cs`
- Modify: `src/LogJammer.Infrastructure/Pipeline/ClassificationProcessor.cs`
- Modify: `src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs`
- Create: `src/LogJammer.Tests/Unit/ML/ClassificationClusterServiceTests.cs`

**Step 1: Define IClassificationClusterService interface**

Create `src/LogJammer.Core/Interfaces/IClassificationClusterService.cs`:

```csharp
namespace LogJammer.Core.Interfaces;

public interface IClassificationClusterService
{
    Task ClusterPendingItemsAsync(CancellationToken ct = default);
}
```

**Step 2: Write failing tests**

Create `src/LogJammer.Tests/Unit/ML/ClassificationClusterServiceTests.cs`:

Tests that exercise:
- Items with similar embeddings get same ClusterId
- Dissimilar items get different ClusterIds (or null)
- Re-clustering after new items arrive
- Reviewed items are excluded from clustering

These will be integration tests (need DB + embedding provider) using `DatabaseFixture` + `OnnxEmbeddingProvider`, following the pattern in `ClassificationQueueMergeTests.cs`.

**Step 3: Implement ClassificationClusterService**

Create `src/LogJammer.Infrastructure/ML/ClassificationClusterService.cs`:

```csharp
using System.Text.Json;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;

namespace LogJammer.Infrastructure.ML;

public class ClassificationClusterService(
    LogJammerDbContext context,
    IClassificationService classificationService,
    IClassificationConfigRepository configRepo,
    ILogger<ClassificationClusterService> logger) : IClassificationClusterService
{
    public async Task ClusterPendingItemsAsync(CancellationToken ct = default)
    {
        var clusteringEnabled = await GetConfigBoolAsync("ClusteringEnabled", true, ct);
        if (!clusteringEnabled) return;

        var threshold = await GetConfigDoubleAsync("ClusteringThreshold", 0.70, ct);
        var weights = await LoadWeightsAsync(ct);

        // Load all unreviewed queue items with their KnownErrors
        var items = await context.ClassificationQueue
            .Include(q => q.KnownError)
            .Where(q => !q.Reviewed)
            .ToListAsync(ct);

        if (items.Count < 2) return;

        // Ensure all have embeddings and features
        foreach (var item in items)
        {
            await classificationService.EnsureEmbeddingAsync(item.KnownError, ct);
            await classificationService.ExtractAndStoreFeaturesAsync(item.KnownError, ct: ct);
        }

        // Reset clusters
        foreach (var item in items)
            item.ClusterId = null;

        var clustered = new HashSet<Guid>();
        var extractor = new ErrorFeatureExtractor();

        foreach (var item in items)
        {
            if (clustered.Contains(item.Id)) continue;

            var clusterId = Guid.NewGuid();
            item.ClusterId = clusterId;
            clustered.Add(item.Id);

            var itemFeatures = DeserializeFeatures(item.KnownError.ExtractedFeatures);
            var itemVector = item.KnownError.EmbeddingVector;
            if (itemVector is null || itemFeatures is null) continue;

            foreach (var candidate in items)
            {
                if (clustered.Contains(candidate.Id)) continue;
                if (candidate.KnownError.EmbeddingVector is null) continue;

                var candidateFeatures = DeserializeFeatures(candidate.KnownError.ExtractedFeatures);
                if (candidateFeatures is null) continue;

                var embeddingSim = 1.0 - CosineDistance(itemVector.ToArray(), candidate.KnownError.EmbeddingVector.ToArray());
                var compositeScore = CompositeScorer.Score(itemFeatures, candidateFeatures, embeddingSim, weights);

                if (compositeScore >= threshold)
                {
                    candidate.ClusterId = clusterId;
                    clustered.Add(candidate.Id);
                }
            }

            // If only one item in cluster, clear the ClusterId (it's a singleton)
            var clusterMembers = items.Where(i => i.ClusterId == clusterId).ToList();
            if (clusterMembers.Count == 1)
                item.ClusterId = null;
        }

        await context.SaveChangesAsync(ct);
        var clusterCount = items.Where(i => i.ClusterId != null).Select(i => i.ClusterId).Distinct().Count();
        logger.LogDebug("Clustering complete: {ClusterCount} clusters from {ItemCount} items", clusterCount, items.Count);
    }

    private static ExtractedFeatures? DeserializeFeatures(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<ExtractedFeatures>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static double CosineDistance(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom > 0 ? 1.0 - dot / denom : 1.0;
    }

    private async Task<ScoringWeights> LoadWeightsAsync(CancellationToken ct)
    {
        return new ScoringWeights(
            await GetConfigDoubleAsync("EmbeddingWeight", 0.50, ct),
            await GetConfigDoubleAsync("TemplateWeight", 0.20, ct),
            await GetConfigDoubleAsync("StructuralWeight", 0.20, ct),
            await GetConfigDoubleAsync("MetadataWeight", 0.10, ct));
    }

    private async Task<double> GetConfigDoubleAsync(string key, double def, CancellationToken ct)
    {
        var c = await configRepo.GetAsync(key, ct);
        return c is not null && double.TryParse(c.Value, out var v) ? v : def;
    }

    private async Task<bool> GetConfigBoolAsync(string key, bool def, CancellationToken ct)
    {
        var c = await configRepo.GetAsync(key, ct);
        return c is not null && bool.TryParse(c.Value, out var v) ? v : def;
    }
}
```

**Step 4: Update ClassificationProcessor to call clustering after batch**

In `src/LogJammer.Infrastructure/Pipeline/ClassificationProcessor.cs`, at the end of `ProcessBatchAsync`, resolve and call the cluster service:

```csharp
var clusterService = scope.ServiceProvider.GetRequiredService<IClassificationClusterService>();
await clusterService.ClusterPendingItemsAsync(ct);
```

**Step 5: Register in DI**

In `src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs`, add:

```csharp
services.AddScoped<IClassificationClusterService, ClassificationClusterService>();
```

**Step 6: Run tests**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~ClassificationCluster" -v n`
Expected: All tests PASS.

**Step 7: Commit**

```bash
git add src/LogJammer.Core/Interfaces/IClassificationClusterService.cs src/LogJammer.Infrastructure/ML/ClassificationClusterService.cs src/LogJammer.Infrastructure/Pipeline/ClassificationProcessor.cs src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs src/LogJammer.Tests/
git commit -m "feat: add classification queue clustering

ClassificationClusterService groups similar pending queue items into
clusters using composite similarity scoring. ClassificationProcessor
runs clustering after each batch processing cycle."
```

---

## Task 6: Cluster-Aware API

**Files:**
- Modify: `src/LogJammer.Api/Dtos/ClassificationDtos.cs`
- Modify: `src/LogJammer.Api/Services/IClassificationQueueService.cs`
- Modify: `src/LogJammer.Api/Services/ClassificationQueueService.cs`
- Modify: `src/LogJammer.Api/Controllers/ClassificationController.cs`

**Step 1: Update DTOs**

In `src/LogJammer.Api/Dtos/ClassificationDtos.cs`:

Add to `ClassificationQueueResponse`:
```csharp
public Guid? ClusterId { get; set; }
public int ClusterSize { get; set; }
```

Add `applyToCluster` to `ApproveClassificationRequest`:
```csharp
public bool ApplyToCluster { get; set; }
```

Add `applyToCluster` to `RejectClassificationRequest`:
```csharp
public bool ApplyToCluster { get; set; }
```

**Step 2: Update service interface**

In `src/LogJammer.Api/Services/IClassificationQueueService.cs`, the existing `ApproveAsync` and `RejectAsync` already accept the request DTOs which now include `ApplyToCluster` — no interface changes needed.

**Step 3: Update ClassificationQueueService**

In `src/LogJammer.Api/Services/ClassificationQueueService.cs`:

- In `MapToResponse`, add ClusterId and compute ClusterSize (query count of items with same ClusterId)
- In `ApproveAsync`, if `request.ApplyToCluster` and item has a ClusterId, find all items in the cluster and approve them all
- In `RejectAsync`, same cluster logic
- Record `ClassificationDecision` on approve/reject

For cluster-aware approve:
```csharp
if (request.ApplyToCluster && item.ClusterId.HasValue)
{
    var clusterItems = await context.ClassificationQueue
        .Where(q => q.ClusterId == item.ClusterId && !q.Reviewed && q.Id != id)
        .ToListAsync(cancellationToken);

    foreach (var clusterItem in clusterItems)
    {
        // Apply same tags
        foreach (var tagId in request.TagIds)
        {
            var exists = await context.ErrorTags
                .AnyAsync(et => et.KnownErrorId == clusterItem.KnownErrorId && et.TagId == tagId, cancellationToken);
            if (!exists)
            {
                context.ErrorTags.Add(new ErrorTag
                {
                    KnownErrorId = clusterItem.KnownErrorId,
                    TagId = tagId,
                    IsAutoAssigned = false,
                    Confidence = 1.0
                });
            }
        }
        clusterItem.Reviewed = true;
        clusterItem.ReviewedAt = DateTime.UtcNow;
    }
}
```

**Step 4: Record ClassificationDecision**

After approve/reject, record the decision:
```csharp
context.ClassificationDecisions.Add(new ClassificationDecision
{
    KnownErrorId = item.KnownErrorId,
    ClusterId = item.ClusterId,
    SimilarityScore = item.Confidence ?? 0,
    Decision = "approve" // or "reject"
});
```

**Step 5: Update MapToResponse for ClusterSize**

```csharp
// In the paged query, compute cluster sizes as a lookup
var clusterSizes = await context.ClassificationQueue
    .Where(q => !q.Reviewed && q.ClusterId != null)
    .GroupBy(q => q.ClusterId)
    .Select(g => new { ClusterId = g.Key, Count = g.Count() })
    .ToDictionaryAsync(g => g.ClusterId!.Value, g => g.Count, cancellationToken);
```

Then in MapToResponse:
```csharp
ClusterId = item.ClusterId,
ClusterSize = item.ClusterId.HasValue && clusterSizes.TryGetValue(item.ClusterId.Value, out var size) ? size : 1,
```

**Step 6: Run existing controller tests + verify build**

Run: `dotnet build src/LogJammer.slnx && dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~ClassificationController" -v n`

**Step 7: Commit**

```bash
git add src/LogJammer.Api/
git commit -m "feat: add cluster-aware approve/reject to classification API

ApproveAsync and RejectAsync now support ApplyToCluster flag that
applies the same tags to all items in the cluster. Responses include
ClusterId and ClusterSize. ClassificationDecision is recorded."
```

---

## Task 7: Frontend — Clustered Queue View

**Files:**
- Modify: `src/frontend/src/api/types.ts`
- Modify: `src/frontend/src/api/hooks/useClassification.ts`
- Modify: `src/frontend/src/pages/Classification.tsx`
- Modify: `src/frontend/src/components/ClassificationQueueCard.tsx`

**Step 1: Update TypeScript types**

In `src/frontend/src/api/types.ts`, add to `ClassificationQueueResponse`:

```typescript
clusterId: string | null;
clusterSize: number;
```

**Step 2: Update hooks**

In `src/frontend/src/api/hooks/useClassification.ts`:

Update `useApproveClassification` mutation to include `applyToCluster`:
```typescript
mutationFn: ({ id, tagIds, applyToCluster = true }: { id: string; tagIds: string[]; applyToCluster?: boolean }) =>
    api.post(`/classification/queue/${id}/approve`, { tagIds, applyToCluster }),
```

Same for `useRejectClassification`:
```typescript
mutationFn: ({ id, correctTagIds, reason, applyToCluster = true }: { id: string; correctTagIds: string[]; reason?: string; applyToCluster?: boolean }) =>
    api.post(`/classification/queue/${id}/reject`, { correctTagIds, reason, applyToCluster }),
```

**Step 3: Update Classification.tsx to group by cluster**

Key changes:
- After filtering, group items by `clusterId` (null clusterId = singleton)
- Render each cluster as a group: show the first item as the "representative", show a `+N similar` badge
- Clicking the badge expands to show all cluster members
- Stats update to show cluster count

```typescript
// Group items by cluster
const grouped = useMemo(() => {
    const clusters = new Map<string, ClassificationQueueResponse[]>();
    const singletons: ClassificationQueueResponse[] = [];

    for (const item of filteredItems) {
        if (item.clusterId) {
            const group = clusters.get(item.clusterId) ?? [];
            group.push(item);
            clusters.set(item.clusterId, group);
        } else {
            singletons.push(item);
        }
    }

    return { clusters: Array.from(clusters.values()), singletons };
}, [filteredItems]);
```

**Step 4: Update ClassificationQueueCard for cluster context**

Add props:
```typescript
interface ClassificationQueueCardProps {
    item: ClassificationQueueResponse;
    clusterItems?: ClassificationQueueResponse[];
}
```

When `clusterItems` is provided:
- Show a `+{clusterItems.length - 1} similar` chip
- "Accept Tags" sends `applyToCluster: true`
- Add an expandable section showing the other cluster members (just message + severity, compact)
- Add a "Classify this one only" secondary action that sends `applyToCluster: false`

**Step 5: Run frontend tests**

Run: `cd src/frontend && npm test`
Expected: Existing tests pass (may need updates for the new props).

**Step 6: Commit**

```bash
git add src/frontend/src/
git commit -m "feat: add clustered view to classification queue

Items are grouped by ClusterId. Each cluster shows a representative
card with +N similar badge. Approve/reject applies to the whole cluster
by default, with 'classify this one only' escape hatch."
```

---

## Task 8: Decision Tracking & Adaptive Threshold Stats

**Files:**
- Create: `src/LogJammer.Core/Interfaces/IClassificationDecisionRepository.cs`
- Create: `src/LogJammer.Infrastructure/Repositories/ClassificationDecisionRepository.cs`
- Modify: `src/LogJammer.Api/Controllers/ClassificationController.cs` (add stats endpoint)
- Modify: `src/LogJammer.Api/Dtos/ClassificationDtos.cs` (add stats DTOs)
- Modify: `src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs`

**Step 1: Create repository interface**

Create `src/LogJammer.Core/Interfaces/IClassificationDecisionRepository.cs`:

```csharp
using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface IClassificationDecisionRepository
{
    Task AddAsync(ClassificationDecision decision, CancellationToken ct = default);
    Task<IReadOnlyList<ClassificationDecision>> GetRecentAsync(int days = 30, CancellationToken ct = default);
    Task<int> GetCountAsync(CancellationToken ct = default);
}
```

**Step 2: Implement repository**

Create `src/LogJammer.Infrastructure/Repositories/ClassificationDecisionRepository.cs`:

```csharp
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class ClassificationDecisionRepository(LogJammerDbContext context) : IClassificationDecisionRepository
{
    public async Task AddAsync(ClassificationDecision decision, CancellationToken ct = default)
    {
        context.ClassificationDecisions.Add(decision);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClassificationDecision>> GetRecentAsync(int days = 30, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await context.ClassificationDecisions
            .AsNoTracking()
            .Where(d => d.CreatedAt >= since)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        return await context.ClassificationDecisions.CountAsync(ct);
    }
}
```

**Step 3: Add stats DTO**

In `src/LogJammer.Api/Dtos/ClassificationDtos.cs`, add:

```csharp
public class ClassificationStatsResponse
{
    public int TotalDecisions { get; set; }
    public double AvgApproveScore { get; set; }
    public double AvgRejectScore { get; set; }
    public double? SuggestedThreshold { get; set; }
    public int ApproveCount { get; set; }
    public int RejectCount { get; set; }
}
```

**Step 4: Add stats endpoint to ClassificationController**

In `src/LogJammer.Api/Controllers/ClassificationController.cs`, add:

```csharp
[HttpGet("stats")]
public async Task<ActionResult<ClassificationStatsResponse>> GetStats(CancellationToken cancellationToken = default)
{
    var decisions = await decisionRepo.GetRecentAsync(30, cancellationToken);
    var approvals = decisions.Where(d => d.Decision == "approve").ToList();
    var rejections = decisions.Where(d => d.Decision == "reject").ToList();

    double? suggestedThreshold = null;
    if (decisions.Count >= 20 && approvals.Count > 0 && rejections.Count > 0)
    {
        // Midpoint between lowest approve and highest reject
        var lowestApprove = approvals.Min(d => d.SimilarityScore);
        var highestReject = rejections.Max(d => d.SimilarityScore);
        suggestedThreshold = Math.Clamp((lowestApprove + highestReject) / 2, 0.5, 0.95);
    }

    return Ok(new ClassificationStatsResponse
    {
        TotalDecisions = decisions.Count,
        AvgApproveScore = approvals.Count > 0 ? approvals.Average(d => d.SimilarityScore) : 0,
        AvgRejectScore = rejections.Count > 0 ? rejections.Average(d => d.SimilarityScore) : 0,
        SuggestedThreshold = suggestedThreshold,
        ApproveCount = approvals.Count,
        RejectCount = rejections.Count
    });
}
```

Update controller constructor to inject `IClassificationDecisionRepository`:
```csharp
public class ClassificationController(
    IClassificationQueueService queueService,
    IClassificationDecisionRepository decisionRepo) : ControllerBase
```

**Step 5: Register in DI**

In `PipelineServiceExtensions.cs`:
```csharp
services.AddScoped<IClassificationDecisionRepository, ClassificationDecisionRepository>();
```

**Step 6: Run build + tests**

Run: `dotnet build src/LogJammer.slnx`

**Step 7: Commit**

```bash
git add src/LogJammer.Core/Interfaces/IClassificationDecisionRepository.cs src/LogJammer.Infrastructure/Repositories/ClassificationDecisionRepository.cs src/LogJammer.Api/ src/LogJammer.Infrastructure/Extensions/
git commit -m "feat: add classification decision tracking and stats endpoint

Records approve/reject decisions with similarity scores. GET /api/classification/stats
returns averages and a suggested optimal threshold based on user behavior."
```

---

## Task 9: Settings UI Update — Scoring Weights & Clustering Config

**Files:**
- Modify: `src/frontend/src/components/settings/ClassificationTab.tsx`
- Modify: `src/frontend/src/api/hooks/useClassification.ts` (add useClassificationStats)
- Modify: `src/frontend/src/api/types.ts` (add ClassificationStatsResponse)

**Step 1: Add stats type and hook**

In `src/frontend/src/api/types.ts`, add:

```typescript
export interface ClassificationStatsResponse {
    totalDecisions: number;
    avgApproveScore: number;
    avgRejectScore: number;
    suggestedThreshold: number | null;
    approveCount: number;
    rejectCount: number;
}
```

In `src/frontend/src/api/hooks/useClassification.ts`, add:

```typescript
export function useClassificationStats() {
    return useQuery({
        queryKey: ['classification', 'stats'],
        queryFn: () => api.get<ClassificationStatsResponse>('/classification/stats'),
    });
}
```

**Step 2: Redesign ClassificationTab**

Replace the current plain table with three sections:

1. **Scoring Weights** — 4 sliders (EmbeddingWeight, TemplateWeight, StructuralWeight, MetadataWeight) with a constraint indicator showing they must sum to 1.0. When one slider changes, proportionally adjust the others.

2. **Clustering** — Toggle for ClusteringEnabled, slider for ClusteringThreshold (0.5–0.95).

3. **Threshold Tuning** — Show stats from `useClassificationStats`: approve/reject counts, average scores, suggested threshold. Button to apply suggested threshold.

Keep the existing key-value table for other configs but filter out the weight/clustering keys (show them in the dedicated sections instead).

**Step 3: Run frontend tests**

Run: `cd src/frontend && npm test`

**Step 4: Commit**

```bash
git add src/frontend/src/
git commit -m "feat: redesign Classification settings with weight sliders and threshold tuning

Three sections: Scoring Weights (sliders summing to 1.0), Clustering
config (enable/threshold), and Threshold Tuning (stats + suggested
threshold from user decision history)."
```

---

## Task 10: SampleLog Mock Elasticsearch Server

**Files:**
- Create: `src/SampleLog/MockEs/MockElasticsearchServer.cs`
- Create: `src/SampleLog/MockEs/EsDocumentGenerator.cs`
- Modify: `src/SampleLog/Models/AppConfig.cs` (add MockEs config)
- Modify: `src/SampleLog/UI/MainWindow.cs` (add [E] shortcut to toggle mock ES)
- Modify: `src/SampleLog/Program.cs` (wire up config)

**Step 1: Add MockEs config model**

In `src/SampleLog/Models/AppConfig.cs`, add:

```csharp
public sealed class MockEsConfig
{
    public int Port { get; set; } = 9200;
    public string IndexName { get; set; } = "app-logs";
    public int DocumentsPerPoll { get; set; } = 20;
}
```

**Step 2: Create EsDocumentGenerator**

Create `src/SampleLog/MockEs/EsDocumentGenerator.cs`:

Generates ELK-format JSON documents using the existing `LogLibrary` templates. Maps each template to structured ELK format:

```json
{
    "@timestamp": "2026-02-18T10:30:00Z",
    "level": "Error",
    "message": "rendered message...",
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

It maintains an internal clock that advances, producing new documents each time `GenerateBatch(since, count)` is called.

**Step 3: Create MockElasticsearchServer**

Create `src/SampleLog/MockEs/MockElasticsearchServer.cs`:

Uses `WebApplication` (minimal API) to expose:

```csharp
var app = WebApplication.Create();

// Root health check
app.MapGet("/", () => new { name = "samplelog-mock-es", cluster_name = "samplelog", status = "green", version = new { number = "8.17.0" } });

// Cluster health
app.MapGet("/_cluster/health", () => new { status = "green", number_of_nodes = 1 });

// Index mapping
app.MapGet("/{index}/_mapping", (string index) => new Dictionary<string, object>
{
    [index] = new { mappings = new { properties = BuildMappingProperties() } }
});

// Search (the main endpoint LogJammer's ES adapter calls)
app.MapPost("/{index}/_search", async (string index, HttpRequest request) =>
{
    // Parse the search body for range query on @timestamp
    // Return documents generated since that timestamp
    // Format: { hits: { total: { value: N }, hits: [ { _source: doc }, ... ] } }
});
```

The server runs in a background task and can be started/stopped from MainWindow.

**Step 4: Add [E] shortcut to MainWindow**

In `src/SampleLog/UI/MainWindow.cs`, add a menu item and keyboard shortcut `[E]` that toggles the mock ES server on/off. When started, show the URL in the status bar.

**Step 5: Wire up in Program.cs**

In `src/SampleLog/Program.cs`, load `MockEs` config section:

```csharp
var mockEsConfig = new MockEsConfig();
config.GetSection("MockEs").Bind(mockEsConfig);
```

Pass to `MainWindow` constructor.

**Step 6: Add SampleLog package reference for ASP.NET minimal API**

The SampleLog project needs `Microsoft.AspNetCore.App` framework reference for the minimal API host. Add to `src/SampleLog/SampleLog.csproj`:

```xml
<ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

**Step 7: Test manually**

```bash
cd src/SampleLog && dotnet run
# Press [E] to start mock ES
# In another terminal:
curl http://localhost:9200/
curl http://localhost:9200/app-logs/_mapping
curl -X POST http://localhost:9200/app-logs/_search -H "Content-Type: application/json" -d '{"query":{"match_all":{}}}'
```

**Step 8: Commit**

```bash
git add src/SampleLog/
git commit -m "feat: add mock Elasticsearch server to SampleLog

Press [E] in TUI to start/stop a mock ES server on port 9200.
Exposes _search, _mapping, and health endpoints with ELK-format
documents generated from the log library templates."
```

---

## Task 11: Integration Testing & Spec Updates

**Files:**
- Modify: `specs/definition-dto.md`
- Modify: `specs/definition-api.md`
- Run full test suite

**Step 1: Update definition-dto.md**

Add:
- `ExtractedFeatures` model
- `ClassificationDecision` entity
- `ScoringWeights` record
- Updated `ClassificationQueueItem` with `ClusterId`
- Updated `KnownError` with `ExtractedFeatures`
- New config entries

**Step 2: Update definition-api.md**

Add:
- `GET /api/classification/stats` endpoint
- Updated request/response schemas for approve/reject with `ApplyToCluster`
- Updated queue response with `ClusterId`, `ClusterSize`

**Step 3: Run full test suite**

```bash
dotnet test src/LogJammer.slnx -v n
cd src/frontend && npm test
```

**Step 4: Commit**

```bash
git add specs/ src/LogJammer.Tests/
git commit -m "docs: update specs and run full integration tests

Update definition-dto.md and definition-api.md with new entities,
models, and endpoint changes from classification pipeline upgrade."
```

---

## Summary of All Changes

| Area | Files Changed | New Files |
|------|--------------|-----------|
| Core Entities | KnownError, ClassificationQueueItem | ClassificationDecision, ExtractedFeatures |
| Infrastructure/ML | ClassificationService | ErrorFeatureExtractor, CompositeScorer, ClassificationClusterService |
| Infrastructure/Pipeline | ClassificationProcessor | — |
| Infrastructure/Data | DbContext, Configurations | ClassificationDecisionConfiguration + migration |
| Infrastructure/Repos | — | ClassificationDecisionRepository |
| API/DTOs | ClassificationDtos | ClassificationStatsResponse |
| API/Services | ClassificationQueueService | — |
| API/Controllers | ClassificationController | — |
| Frontend/types | types.ts | ClassificationStatsResponse |
| Frontend/hooks | useClassification.ts | useClassificationStats |
| Frontend/pages | Classification.tsx | — |
| Frontend/components | ClassificationQueueCard.tsx, ClassificationTab.tsx | — |
| SampleLog | Program.cs, MainWindow.cs, AppConfig.cs | MockElasticsearchServer, EsDocumentGenerator |
| Specs | definition-dto.md, definition-api.md | — |
