using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController(ITagService tagService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> GetAll(CancellationToken cancellationToken = default)
    {
        var tags = await tagService.GetAllAsync(cancellationToken);
        return Ok(tags);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await tagService.GetByIdAsync(id, cancellationToken);
        if (tag is null) return NotFound();
        return Ok(tag);
    }

    [HttpPost]
    public async Task<ActionResult<TagResponse>> Create([FromBody] CreateTagRequest request, CancellationToken cancellationToken = default)
    {
        var tag = await tagService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagResponse>> Update(Guid id, [FromBody] UpdateTagRequest request, CancellationToken cancellationToken = default)
    {
        var tag = await tagService.UpdateAsync(id, request, cancellationToken);
        if (tag is null) return NotFound();
        return Ok(tag);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await tagService.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
