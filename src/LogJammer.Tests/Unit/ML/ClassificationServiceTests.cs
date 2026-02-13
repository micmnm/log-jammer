using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.ML;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;

namespace LogJammer.Tests.Unit.ML;

public class ClassificationServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammerDbContext _context = null!;
    private ClassificationService _service = null!;
    private OnnxEmbeddingProvider _embeddingProvider = null!;
    private DataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        Skip.IfNot(TestDatabaseProvider.IsDockerAvailable(), "Docker is not available");

        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();

        var modelDir = Path.Combine(Path.GetTempPath(), "logjammer-test-models", "all-MiniLM-L6-v2");
        var downloader = new ModelDownloader(modelDir, NullLogger<ModelDownloader>.Instance);
        _embeddingProvider = new OnnxEmbeddingProvider(downloader, NullLogger<OnnxEmbeddingProvider>.Instance);

        var configRepo = new ClassificationConfigRepository(_context);
        var overrideRepo = new UserOverrideRepository(_context);

        // Seed default config
        await configRepo.UpsertAsync("SimilarityThreshold", "0.85", "test");
        await configRepo.UpsertAsync("AutoTagConfidenceThreshold", "0.7", "test");
        await configRepo.UpsertAsync("MaxSuggestedTags", "3", "test");

        _service = new ClassificationService(
            _context,
            _embeddingProvider,
            configRepo,
            overrideRepo,
            NullLogger<ClassificationService>.Instance);

        // Create a data source
        _dataSource = new DataSource
        {
            Name = "Test Source",
            AdapterType = Core.Enums.AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        _context.DataSources.Add(_dataSource);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_embeddingProvider is not null)
            _embeddingProvider.Dispose();
        if (_context is not null)
            await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [SkippableFact]
    public async Task ClassifyAsync_ShouldGenerateEmbedding()
    {
        var error = new KnownError
        {
            FingerprintHash = "test-hash-1",
            RepresentativeMessage = "NullReferenceException in UserService.GetUser",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error);
        await _context.SaveChangesAsync();

        var result = await _service.ClassifyAsync(error);

        // Reload to check embedding was stored
        var reloaded = await _context.KnownErrors.FirstAsync(e => e.Id == error.Id);
        reloaded.EmbeddingVector.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task ClassifyAsync_SimilarErrors_ShouldHaveHighSimilarity()
    {
        // Create first error with embedding
        var error1 = new KnownError
        {
            FingerprintHash = "test-hash-sim-1",
            RepresentativeMessage = "NullReferenceException in UserService.GetUser at line 42",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error1);
        await _context.SaveChangesAsync();

        await _service.ClassifyAsync(error1);

        // Create a very similar error
        var error2 = new KnownError
        {
            FingerprintHash = "test-hash-sim-2",
            RepresentativeMessage = "NullReferenceException in UserService.GetUser at line 45",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error2);
        await _context.SaveChangesAsync();

        var result = await _service.ClassifyAsync(error2);

        result.SimilarityScore.Should().BeGreaterThan(0.7);
    }

    [SkippableFact]
    public async Task ClassifyAsync_DissimilarErrors_ShouldHaveLowSimilarity()
    {
        var error1 = new KnownError
        {
            FingerprintHash = "test-hash-dis-1",
            RepresentativeMessage = "NullReferenceException in UserService.GetUser",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error1);
        await _context.SaveChangesAsync();

        await _service.ClassifyAsync(error1);

        var error2 = new KnownError
        {
            FingerprintHash = "test-hash-dis-2",
            RepresentativeMessage = "Redis connection timeout after 30 seconds to cache cluster",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error2);
        await _context.SaveChangesAsync();

        var result = await _service.ClassifyAsync(error2);

        result.SimilarityScore.Should().BeLessThan(0.85);
    }

    [SkippableFact]
    public async Task ClassifyAsync_WithPinnedOverride_ShouldReturnOverrideTags()
    {
        // Create a tag
        var tag = new Tag { Name = "test-tag", TagType = "auto" };
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        var error = new KnownError
        {
            FingerprintHash = "test-hash-override",
            RepresentativeMessage = "Some error",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error);
        await _context.SaveChangesAsync();

        // Add a pinned classification override
        _context.UserOverrides.Add(new UserOverride
        {
            KnownErrorId = error.Id,
            OverrideType = "classification",
            OverrideData = System.Text.Json.JsonSerializer.Serialize(new[] { tag.Id }),
            Reason = "test pin"
        });
        await _context.SaveChangesAsync();

        var result = await _service.ClassifyAsync(error);

        result.NeedsReview.Should().BeFalse();
        result.SuggestedTags.Should().HaveCount(1);
        result.SuggestedTags[0].TagId.Should().Be(tag.Id);
        result.SuggestedTags[0].Confidence.Should().Be(1.0);
    }

    [SkippableFact]
    public async Task RecalculateTagCentroidAsync_ShouldCreateCentroid()
    {
        var tag = new Tag { Name = "centroid-test-tag", TagType = "auto" };
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        // Create an error with embedding and tag it
        var error = new KnownError
        {
            FingerprintHash = "test-hash-centroid",
            RepresentativeMessage = "Database connection error",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error);
        await _context.SaveChangesAsync();

        await _service.ClassifyAsync(error);

        _context.ErrorTags.Add(new ErrorTag
        {
            KnownErrorId = error.Id,
            TagId = tag.Id,
            IsAutoAssigned = false,
            Confidence = 1.0
        });
        await _context.SaveChangesAsync();

        await _service.RecalculateTagCentroidAsync(tag.Id);

        var centroid = await _context.TagCentroids.FirstOrDefaultAsync(tc => tc.TagId == tag.Id);
        centroid.Should().NotBeNull();
        centroid!.CentroidVector.Should().NotBeNull();
        centroid.ErrorCount.Should().Be(1);
    }
}
