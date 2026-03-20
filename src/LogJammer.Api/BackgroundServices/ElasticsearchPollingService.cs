using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LogJammer.Api.BackgroundServices;

public class ElasticsearchPollingService(
    IServiceScopeFactory scopeFactory,
    IngestionPipeline pipeline,
    ILogger<ElasticsearchPollingService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllSourcesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in Elasticsearch polling service");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PollAllSourcesAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

        var sources = await db.DataSources
            .Where(d => d.Enabled && d.Type == DataSourceType.Elasticsearch)
            .ToListAsync(stoppingToken);

        foreach (var dataSource in sources)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await PollSourceAsync(dataSource, db, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling data source {DataSourceId} ({Name})", dataSource.Id, dataSource.Name);
            }
        }
    }

    private async Task PollSourceAsync(DataSource dataSource, LogJammerDbContext db, CancellationToken stoppingToken)
    {
        var since = dataSource.LastPolledAt ?? DateTimeOffset.UtcNow.AddMinutes(-5);

        ElasticsearchClientSettings clientSettings;
        string indexPattern = "*";

        // Parse connection config (may be plain URL or JSON with url/index)
        try
        {
            using var doc = JsonDocument.Parse(dataSource.ConnectionConfig);
            var root = doc.RootElement;
            var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : dataSource.ConnectionConfig;
            indexPattern = root.TryGetProperty("index", out var indexProp) ? indexProp.GetString() ?? "*" : "*";
            clientSettings = new ElasticsearchClientSettings(new Uri(url ?? dataSource.ConnectionConfig));
        }
        catch
        {
            // Treat connection config as plain URL
            clientSettings = new ElasticsearchClientSettings(new Uri(dataSource.ConnectionConfig));
        }

        var client = new ElasticsearchClient(clientSettings);

        var response = await client.SearchAsync<JsonElement>(s => s
            .Indices(indexPattern)
            .Size(500)
            .Query(q => q
                .Range(r => r
                    .Date(dr => dr
                        .Field("@timestamp")
                        .Gt(since.ToString("o")))))
            .Sort(sort => sort.Field("@timestamp", f => f.Order(SortOrder.Asc))),
            stoppingToken);

        if (!response.IsSuccess())
        {
            logger.LogWarning(
                "ES search failed for source {DataSourceId}: {Debug}",
                dataSource.Id,
                response.DebugInformation);
            return;
        }

        if (response.Hits.Count == 0)
        {
            dataSource.LastPolledAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            return;
        }

        var entries = new List<RawLogEntry>(response.Hits.Count);
        DateTimeOffset latestTimestamp = since;

        foreach (var hit in response.Hits)
        {
            var hitSource = hit.Source;

            // Auto-detect timestamp field
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;
            if (hitSource.TryGetProperty("@timestamp", out var tsEl))
            {
                if (tsEl.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(tsEl.GetString(), out var parsed))
                {
                    timestamp = parsed;
                }
            }

            if (timestamp > latestTimestamp)
                latestTimestamp = timestamp;

            // Auto-detect message field
            string message = string.Empty;
            foreach (var msgField in new[] { "message", "log", "msg" })
            {
                if (hitSource.TryGetProperty(msgField, out var msgEl) &&
                    msgEl.ValueKind == JsonValueKind.String)
                {
                    message = msgEl.GetString() ?? string.Empty;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(message))
                continue;

            // Auto-detect level field
            string? level = null;
            foreach (var levelField in new[] { "level", "severity", "log.level", "loglevel" })
            {
                if (hitSource.TryGetProperty(levelField, out var levelEl) &&
                    levelEl.ValueKind == JsonValueKind.String)
                {
                    level = levelEl.GetString();
                    break;
                }
            }

            // Build fields for message template substitution
            Dictionary<string, string>? fields = null;
            if (dataSource.MessageTemplate is not null)
            {
                fields = new Dictionary<string, string>();
                foreach (var prop in hitSource.EnumerateObject())
                {
                    fields[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? string.Empty
                        : prop.Value.ToString();
                }
            }

            entries.Add(new RawLogEntry
            {
                Message = message,
                Timestamp = timestamp,
                Level = level,
                Fields = fields,
            });
        }

        if (entries.Count > 0)
        {
            await pipeline.ProcessEntriesAsync(entries, dataSource.Id, dataSource.MessageTemplate);
            logger.LogInformation(
                "Processed {Count} entries from source {DataSourceId} ({Name})",
                entries.Count,
                dataSource.Id,
                dataSource.Name);
        }

        dataSource.LastPolledAt = latestTimestamp;
        await db.SaveChangesAsync(stoppingToken);
    }
}
