using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpikeDetectionRulesController(ISpikeDetectionRuleService ruleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SpikeDetectionRuleDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var rules = await ruleService.GetAllAsync(cancellationToken);
        return Ok(rules);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpikeDetectionRuleDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await ruleService.GetByIdAsync(id, cancellationToken);
        if (rule is null) return NotFound();
        return Ok(rule);
    }

    [HttpPost]
    public async Task<ActionResult<SpikeDetectionRuleDto>> Create([FromBody] CreateSpikeDetectionRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await ruleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SpikeDetectionRuleDto>> Update(Guid id, [FromBody] UpdateSpikeDetectionRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await ruleService.UpdateAsync(id, request, cancellationToken);
        if (rule is null) return NotFound();
        return Ok(rule);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await ruleService.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
