using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.ML;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LogJammer.Tests.Unit.Pipeline;

public class LogIngestionPipelineTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammer.Infrastructure.Data.LogJammerDbContext _context = null!;
    private OnnxEmbeddingProvider? _embeddingProvider;

    public async Task InitializeAsync()
    {
        Skip.IfNot(TestDatabaseProvider.IsDockerAvailable(), "Docker is not available");
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _embeddingProvider?.Dispose();
        if (_context is not null) await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [SkippableFact]
    public async Task ProcessEntries_NewEntry_CreatesKnownErrorAndQueuesClassification()
    {
        var dataSource = new DataSource
        {
            Name = "Test KibanaProxy",
            AdapterType = AdapterType.KibanaProxy,
            ConnectionConfig = "{}",
            Enabled = true,
            PollIntervalSeconds = 60,
            SamplingBudget = 500
        };
        _context.DataSources.Add(dataSource);
        await _context.SaveChangesAsync();

        var pipeline = CreatePipeline();
        var entries = new List<RawLogEntry>
        {
            new(DateTime.UtcNow, new Dictionary<string, object?>
            {
                ["message"] = "NullReferenceException in UserService",
                ["level"] = "Error"
            })
        };

        var result = await pipeline.ProcessEntriesAsync(dataSource, entries, 1.0);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Duplicates);

        var knownErrors = await _context.KnownErrors.Where(ke => ke.DataSourceId == dataSource.Id).ToListAsync();
        Assert.Single(knownErrors);

        var queueItems = await _context.ClassificationQueue.Where(q => q.KnownErrorId == knownErrors[0].Id).ToListAsync();
        Assert.Single(queueItems);
    }

    [SkippableFact]
    public async Task ProcessEntries_DuplicateEntry_IncrementsOccurrences()
    {
        var dataSource = new DataSource
        {
            Name = "Test KibanaProxy 2",
            AdapterType = AdapterType.KibanaProxy,
            ConnectionConfig = "{}",
            Enabled = true,
            PollIntervalSeconds = 60,
            SamplingBudget = 500
        };
        _context.DataSources.Add(dataSource);
        await _context.SaveChangesAsync();

        var pipeline = CreatePipeline();
        var entries = new List<RawLogEntry>
        {
            new(DateTime.UtcNow, new Dictionary<string, object?>
            {
                ["message"] = "Timeout connecting to database",
                ["level"] = "Error"
            })
        };

        await pipeline.ProcessEntriesAsync(dataSource, entries, 1.0);
        var result = await pipeline.ProcessEntriesAsync(dataSource, entries, 1.0);

        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.Duplicates);

        var knownError = await _context.KnownErrors
            .Where(ke => ke.DataSourceId == dataSource.Id)
            .SingleAsync();
        Assert.Equal(2, knownError.TotalOccurrences);
    }

    private LogIngestionPipeline CreatePipeline()
    {
        var schemaMapper = new SchemaMapper();
        var fingerprintCalculator = new FingerprintCalculator();
        var knownErrorRepo = new KnownErrorRepository(_context);
        var occurrenceRepo = new ErrorOccurrenceRepository(_context);
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        var configRepo = new ClassificationConfigRepository(_context);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogIngestionPipeline>.Instance;
        return new LogIngestionPipeline(schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo, _context, embeddingProvider, configRepo, logger);
    }

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

        // Seed the config to enable embedding-based grouping
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

        // Second entry — different wording but same semantic error
        // (must normalize to a different fingerprint hash so the embedding fallback is used)
        var entries2 = new List<RawLogEntry>
        {
            new(DateTime.UtcNow, new Dictionary<string, object?>
            {
                ["message"] = "The upstream server returned BadGateway for request to example.ngrok-free.dev",
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
}
