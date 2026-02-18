using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class IngestController(IIngestService ingestService) : ControllerBase
{
    [HttpPost("{dataSourceId:guid}")]
    public async Task<ActionResult<IngestResponse>> Ingest(
        Guid dataSourceId,
        [FromBody] IngestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = request.Entries
                .Select(e => (e.Timestamp, e.Fields))
                .ToList();

            var (accepted, duplicates, failed) = await ingestService.IngestAsync(
                dataSourceId, entries, cancellationToken);

            return Ok(new IngestResponse
            {
                Accepted = accepted,
                Duplicates = duplicates,
                Failed = failed
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: 404);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
    }
}
