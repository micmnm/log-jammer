using System.Text.Json;
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController(LogJammerDbContext db, IngestionPipeline pipeline) : ControllerBase
{
    [HttpPost("{dataSourceId:guid}")]
    public async Task<ActionResult<IngestResponse>> Ingest(Guid dataSourceId, [FromBody] IngestRequest request)
    {
        var source = await db.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId);
        if (source is null)
            return NotFound(new { message = "Data source not found" });

        if (!source.Enabled)
            return BadRequest(new { message = "Data source is disabled" });

        // Poll interval guard: reject if another client polled too recently
        if (source.Type == DataSourceType.KibanaProxy && source.LastPolledAt.HasValue)
        {
            var pollIntervalMinutes = ExtractPollIntervalMinutes(source.ConnectionConfig);
            if (pollIntervalMinutes.HasValue)
            {
                var timeSinceLastPoll = DateTimeOffset.UtcNow - source.LastPolledAt.Value;
                var threshold = TimeSpan.FromMinutes(pollIntervalMinutes.Value * 0.5);
                if (timeSinceLastPoll < threshold)
                {
                    var remaining = threshold - timeSinceLastPoll;
                    return Ok(new IngestResponse(
                        Accepted: 0,
                        Skipped: true,
                        Reason: $"Another client polled {timeSinceLastPoll.TotalSeconds:F0}s ago, next window in {remaining.TotalSeconds:F0}s"));
                }
            }
        }

        var entries = request.Entries.Select(e => new RawLogEntry
        {
            Message = e.Message,
            Timestamp = e.Timestamp,
            Level = e.Level,
        }).ToList();

        await pipeline.ProcessEntriesAsync(entries, dataSourceId, source.MessageTemplate);

        // Update LastPolledAt — ignore concurrency conflicts since this is just a timestamp update
        source.LastPolledAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another client updated the DataSource concurrently — the ingest still succeeded,
            // so just log and continue. LastPolledAt will be updated on the next successful ingest.
        }

        return Ok(new IngestResponse(entries.Count));
    }

    private static double? ExtractPollIntervalMinutes(string connectionConfig)
    {
        try
        {
            using var doc = JsonDocument.Parse(connectionConfig);
            if (doc.RootElement.TryGetProperty("pollIntervalMinutes", out var prop))
                return prop.GetDouble();
        }
        catch (JsonException)
        {
            // ConnectionConfig is not JSON (e.g., plain URL for Elasticsearch) — no poll interval
        }
        return null;
    }
}
