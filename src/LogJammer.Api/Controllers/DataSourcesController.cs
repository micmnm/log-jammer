using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataSourcesController(IDataSourceService dataSourceService) : ControllerBase
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
        if (dataSource is null) return NotFound();
        return Ok(dataSource);
    }

    [HttpPost]
    public async Task<ActionResult<DataSourceResponse>> Create(
        [FromBody] CreateDataSourceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var created = await dataSourceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DataSourceResponse>> Update(
        Guid id,
        [FromBody] UpdateDataSourceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await dataSourceService.UpdateAsync(id, request, cancellationToken);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await dataSourceService.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<ConnectionTestResponse>> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        var result = await dataSourceService.TestConnectionAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<ActionResult<SchemaResponse>> GetSchema(Guid id, CancellationToken cancellationToken)
    {
        var schema = await dataSourceService.GetSchemaAsync(id, cancellationToken);
        if (schema is null) return NotFound();
        return Ok(schema);
    }

    [HttpGet("{id:guid}/sample")]
    public async Task<ActionResult<SampleRecordsResponse>> GetSampleRecords(
        Guid id,
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var records = await dataSourceService.GetSampleRecordsAsync(id, count, cancellationToken);
        if (records is null) return NotFound();
        return Ok(records);
    }
}
