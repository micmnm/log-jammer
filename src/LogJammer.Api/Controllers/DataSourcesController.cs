using Elastic.Clients.Elasticsearch;
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/datasources")]
public class DataSourcesController(LogJammerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DataSourceResponse>>> GetAll()
    {
        var sources = await db.DataSources
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync();

        return Ok(sources.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DataSourceResponse>> GetById(Guid id)
    {
        var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (source is null)
            return NotFound();

        return Ok(ToResponse(source));
    }

    [HttpPost]
    public async Task<ActionResult<DataSourceResponse>> Create([FromBody] CreateDataSourceRequest request)
    {
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            ConnectionConfig = request.ConnectionConfig,
            MessageTemplate = request.MessageTemplate,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = source.Id }, ToResponse(source));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DataSourceResponse>> Update(Guid id, [FromBody] UpdateDataSourceRequest request)
    {
        var source = await db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
        if (source is null)
            return NotFound();

        if (request.Version != source.Version)
            return Conflict(new { error = "conflict", message = "DataSource was modified by another client", currentVersion = source.Version });

        if (request.Name is not null)
            source.Name = request.Name;
        if (request.ConnectionConfig is not null)
            source.ConnectionConfig = request.ConnectionConfig;
        if (request.MessageTemplate is not null)
            source.MessageTemplate = request.MessageTemplate;
        if (request.Enabled.HasValue)
            source.Enabled = request.Enabled.Value;

        source.Version++;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await db.DataSources.AsNoTracking().FirstAsync(d => d.Id == id);
            return Conflict(new { error = "conflict", message = "DataSource was modified by another client", currentVersion = current.Version });
        }

        return Ok(ToResponse(source));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var source = await db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
        if (source is null)
            return NotFound();

        db.DataSources.Remove(source);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestConnection(Guid id)
    {
        var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (source is null)
            return NotFound();

        if (source.Type != DataSourceType.Elasticsearch)
            return BadRequest(new { message = "Connection test is only supported for Elasticsearch data sources" });

        try
        {
            var settings = new ElasticsearchClientSettings(new Uri(source.ConnectionConfig));
            var client = new ElasticsearchClient(settings);
            var response = await client.PingAsync();
            if (response.IsSuccess())
                return Ok(new { success = true });

            return Ok(new { success = false, message = response.DebugInformation });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    private static DataSourceResponse ToResponse(DataSource source) => new(
        source.Id,
        source.Name,
        source.Type,
        source.ConnectionConfig,
        source.MessageTemplate,
        source.Enabled,
        source.CreatedAt,
        source.LastPolledAt,
        source.Version);
}
