using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using LogJammer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataSourcesController(
    IDataSourceService dataSourceService,
    ILogFileDetectService logFileDetectService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DataSourceResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var dataSources = await dataSourceService.GetAllAsync(cancellationToken);
        return Ok(dataSources);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DataSourceResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dataSource = await dataSourceService.GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return Problem(detail: "Data source not found.", statusCode: 404);
        return Ok(dataSource);
    }

    [HttpPost]
    public async Task<ActionResult<DataSourceResponse>> Create(
        [FromBody] CreateDataSourceRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dataSourceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DataSourceResponse>> Update(
        Guid id,
        [FromBody] UpdateDataSourceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await dataSourceService.UpdateAsync(id, request, cancellationToken);
        if (updated is null) return Problem(detail: "Data source not found.", statusCode: 404);
        return Ok(updated);
    }

    [HttpGet("{id:guid}/deletion-impact")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(Guid id, CancellationToken cancellationToken)
    {
        var impact = await dataSourceService.GetDeletionImpactAsync(id, cancellationToken);
        if (impact is null) return Problem(detail: "Data source not found.", statusCode: 404);
        return Ok(impact);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] bool preserveHistory = false,
        CancellationToken cancellationToken = default)
    {
        var deleted = await dataSourceService.DeleteAsync(id, preserveHistory, cancellationToken);
        if (!deleted) return Problem(detail: "Data source not found.", statusCode: 404);
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<ConnectionTestResponse>> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        var result = await dataSourceService.TestConnectionAsync(id, cancellationToken);
        if (result is null) return Problem(detail: "Data source not found.", statusCode: 404);
        return Ok(result);
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<ActionResult<SchemaResponse>> GetSchema(Guid id, CancellationToken cancellationToken)
    {
        var schema = await dataSourceService.GetSchemaAsync(id, cancellationToken);
        if (schema is null) return Problem(detail: "Data source not found.", statusCode: 404);
        return Ok(schema);
    }

    [HttpGet("{id:guid}/sample")]
    public async Task<ActionResult<SampleRecordsResponse>> GetSampleRecords(
        Guid id,
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var records = await dataSourceService.GetSampleRecordsAsync(id, count, cancellationToken);
        if (records is null) return Problem(detail: "Data source not found.", statusCode: 404);
        return Ok(records);
    }

    [HttpPost("discover/indices")]
    public async Task<ActionResult<DiscoverIndicesResponse>> DiscoverIndices(
        [FromBody] DiscoverIndicesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dataSourceService.DiscoverIndicesAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
        catch (Exception ex)
        {
            return Problem(detail: $"Discovery failed: {ex.Message}", statusCode: 502);
        }
    }

    [HttpPost("discover/schema")]
    public async Task<ActionResult<SchemaResponse>> DiscoverSchema(
        [FromBody] DiscoverSchemaRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dataSourceService.DiscoverSchemaAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
        catch (Exception ex)
        {
            return Problem(detail: $"Schema discovery failed: {ex.Message}", statusCode: 502);
        }
    }

    [HttpPost("detect")]
    public async Task<ActionResult<DetectResponse>> Detect(
        [FromBody] DetectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await logFileDetectService.DetectAsync(request.FilePath, cancellationToken);
            return Ok(new DetectResponse
            {
                DetectedFormat = result.DetectedFormat,
                Fields = result.Fields.Select(f => new DetectedFieldDto
                {
                    Name = f.Name,
                    Type = f.Type,
                    ProposedRole = f.ProposedRole
                }).ToList(),
                SampleRecords = result.SampleRecords,
                ProposedConfig = new DetectedConfigDto
                {
                    FilePath = result.ProposedConfig.FilePath,
                    ParseMode = result.ProposedConfig.ParseMode,
                    TimestampField = result.ProposedConfig.TimestampField,
                    LevelField = result.ProposedConfig.LevelField,
                    MessageField = result.ProposedConfig.MessageField,
                    RegexPattern = result.ProposedConfig.RegexPattern
                }
            });
        }
        catch (FileNotFoundException)
        {
            return Problem(detail: "File not found.", statusCode: 404);
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(detail: "File path is not in an allowed directory.", statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
    }
}
