using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using LogJammer.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ErrorGroupsController(IErrorGroupService errorGroupService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? dataSourceId,
        [FromQuery] ErrorStatus? status,
        [FromQuery] ErrorSeverity? severity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await errorGroupService.GetAllAsync(dataSourceId, status, severity, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await errorGroupService.GetByIdAsync(id, cancellationToken);
        return result is null ? Problem(detail: "Error group not found.", statusCode: 404) : Ok(result);
    }

    [HttpGet("{id:guid}/occurrences")]
    public async Task<IActionResult> GetOccurrences(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var result = await errorGroupService.GetOccurrencesAsync(id, from, to, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateErrorGroupStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await errorGroupService.UpdateStatusAsync(id, request.Status, cancellationToken);
        return result is null ? Problem(detail: "Error group not found.", statusCode: 404) : Ok(result);
    }

    [HttpPut("{id:guid}/severity")]
    public async Task<IActionResult> UpdateSeverity(Guid id, [FromBody] UpdateErrorGroupSeverityRequest request, CancellationToken cancellationToken)
    {
        var result = await errorGroupService.UpdateSeverityAsync(id, request.Severity, cancellationToken);
        return result is null ? Problem(detail: "Error group not found.", statusCode: 404) : Ok(result);
    }
}
