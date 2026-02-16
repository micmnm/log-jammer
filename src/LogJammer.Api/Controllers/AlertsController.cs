using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using LogJammer.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController(IAlertService alertService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AlertListResponse>> GetAll(
        [FromQuery] AlertStatus? status = null,
        [FromQuery] Guid? dataSourceId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await alertService.GetAllAsync(status, dataSourceId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await alertService.GetByIdAsync(id, cancellationToken);
        if (alert is null) return Problem(detail: "Alert not found.", statusCode: 404);
        return Ok(alert);
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<AlertDto>> Acknowledge(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await alertService.AcknowledgeAsync(id, cancellationToken);
        if (alert is null) return Problem(detail: "Alert not found.", statusCode: 404);
        return Ok(alert);
    }

    [HttpGet("history")]
    public async Task<ActionResult<AlertListResponse>> GetHistory(
        [FromQuery] Guid? dataSourceId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await alertService.GetHistoryAsync(dataSourceId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("correlated")]
    public async Task<ActionResult<IReadOnlyList<CorrelatedSpikeAlertDto>>> GetCorrelated(
        [FromQuery] AlertStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await alertService.GetCorrelatedAlertsAsync(status, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
