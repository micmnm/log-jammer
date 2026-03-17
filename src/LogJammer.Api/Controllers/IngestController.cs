using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
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
        var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dataSourceId);
        if (source is null)
            return NotFound(new { message = "Data source not found" });

        if (!source.Enabled)
            return BadRequest(new { message = "Data source is disabled" });

        var entries = request.Entries.Select(e => new RawLogEntry
        {
            Message = e.Message,
            Timestamp = e.Timestamp,
            Level = e.Level,
        }).ToList();

        await pipeline.ProcessEntriesAsync(entries, dataSourceId, source.MessageTemplate);

        return Ok(new IngestResponse(entries.Count));
    }
}
