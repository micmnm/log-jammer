using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests.Unit.Pipeline;

public class LogIngestionPipelineTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammer.Infrastructure.Data.LogJammerDbContext _context = null!;

    public async Task InitializeAsync()
    {
        Skip.IfNot(TestDatabaseProvider.IsDockerAvailable(), "Docker is not available");
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogIngestionPipeline>.Instance;
        return new LogIngestionPipeline(schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo, _context, logger);
    }
}
