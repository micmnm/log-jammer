using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClassificationController(IClassificationQueueService queueService) : ControllerBase
{
    [HttpGet("queue")]
    public async Task<ActionResult<ClassificationQueuePagedResponse>> GetQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await queueService.GetPendingAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("queue/{id:guid}")]
    public async Task<ActionResult<ClassificationQueueResponse>> GetQueueItem(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await queueService.GetByIdAsync(id, cancellationToken);
        if (item is null) return Problem(detail: "Classification queue item not found.", statusCode: 404);
        return Ok(item);
    }

    [HttpPost("queue/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveClassificationRequest request, CancellationToken cancellationToken = default)
    {
        var success = await queueService.ApproveAsync(id, request, cancellationToken);
        if (!success) return Problem(detail: "Classification queue item not found.", statusCode: 404);
        return NoContent();
    }

    [HttpPost("queue/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectClassificationRequest request, CancellationToken cancellationToken = default)
    {
        var success = await queueService.RejectAsync(id, request, cancellationToken);
        if (!success) return Problem(detail: "Classification queue item not found.", statusCode: 404);
        return NoContent();
    }
}
