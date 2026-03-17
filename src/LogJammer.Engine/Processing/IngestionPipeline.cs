using System.Collections.Concurrent;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Drain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Engine.Processing;

public class IngestionPipeline(IServiceScopeFactory scopeFactory, DrainConfig? drainConfig = null)
{
    private readonly DrainConfig _drainConfig = drainConfig ?? new DrainConfig();
    private readonly ConcurrentDictionary<Guid, DrainParser> _parsers = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task ProcessEntriesAsync(
        IEnumerable<RawLogEntry> entries,
        Guid dataSourceId,
        string? messageTemplate)
    {
        var semaphore = _locks.GetOrAdd(dataSourceId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var parser = await GetOrCreateParserAsync(dataSourceId);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var patternStore = new PatternStore(db);

            foreach (var entry in entries)
            {
                string message;
                if (entry.Fields is not null && messageTemplate is not null)
                {
                    var processedFields = StackTracePreprocessor.Process(entry.Fields);
                    message = MessageTemplateApplier.Apply(messageTemplate, processedFields);
                }
                else
                {
                    message = entry.Message;
                }

                var severity = SeverityMapper.Map(entry.Level);
                var result = parser.ParseLogMessage(message);
                await patternStore.RecordOccurrenceAsync(result, severity, message, dataSourceId, entry.Timestamp);
            }

            await PersistDrainStateAsync(parser, dataSourceId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<DrainParser> GetOrCreateParserAsync(Guid dataSourceId)
    {
        if (_parsers.TryGetValue(dataSourceId, out var existing))
        {
            return existing;
        }

        var parser = new DrainParser(_drainConfig);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

        var savedState = await db.DrainStates
            .FirstOrDefaultAsync(s => s.DataSourceId == dataSourceId);

        if (savedState is not null)
        {
            parser.RestoreState(savedState.SerializedState);
        }

        _parsers[dataSourceId] = parser;
        return parser;
    }

    private async Task PersistDrainStateAsync(DrainParser parser, Guid dataSourceId)
    {
        var stateBytes = parser.GetState();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

        var existing = await db.DrainStates
            .FirstOrDefaultAsync(s => s.DataSourceId == dataSourceId);

        if (existing is null)
        {
            db.DrainStates.Add(new DrainState
            {
                Id = Guid.NewGuid(),
                DataSourceId = dataSourceId,
                SerializedState = stateBytes,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.SerializedState = stateBytes;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}
