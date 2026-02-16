using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/datasources/{dataSourceId:guid}/fingerprint-configs")]
public class FingerprintConfigsController(IFingerprintConfigService configService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FingerprintConfigResponse>>> GetAll(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        var configs = await configService.GetByDataSourceIdAsync(dataSourceId, cancellationToken);
        return Ok(configs);
    }

    [HttpPost]
    public async Task<ActionResult<FingerprintConfigResponse>> Create(Guid dataSourceId, [FromBody] CreateFingerprintConfigRequest request, CancellationToken cancellationToken = default)
    {
        var config = await configService.CreateAsync(dataSourceId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { dataSourceId, id = config.Id }, config);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FingerprintConfigResponse>> GetById(Guid dataSourceId, Guid id, CancellationToken cancellationToken = default)
    {
        var config = await configService.GetByIdAsync(id, cancellationToken);
        if (config is null || config.DataSourceId != dataSourceId) return Problem(detail: "Fingerprint config not found.", statusCode: 404);
        return Ok(config);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FingerprintConfigResponse>> Update(Guid dataSourceId, Guid id, [FromBody] UpdateFingerprintConfigRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await configService.GetByIdAsync(id, cancellationToken);
        if (existing is null || existing.DataSourceId != dataSourceId) return Problem(detail: "Fingerprint config not found.", statusCode: 404);

        var config = await configService.UpdateAsync(id, request, cancellationToken);
        return Ok(config);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid dataSourceId, Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await configService.GetByIdAsync(id, cancellationToken);
        if (existing is null || existing.DataSourceId != dataSourceId) return Problem(detail: "Fingerprint config not found.", statusCode: 404);

        await configService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
