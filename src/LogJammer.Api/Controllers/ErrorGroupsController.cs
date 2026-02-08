using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErrorGroupsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => StatusCode(501, "Not implemented");

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id) => StatusCode(501, "Not implemented");

    [HttpGet("{id:guid}/occurrences")]
    public IActionResult GetOccurrences(Guid id) => StatusCode(501, "Not implemented");

    [HttpPut("{id:guid}/status")]
    public IActionResult UpdateStatus(Guid id) => StatusCode(501, "Not implemented");

    [HttpPut("{id:guid}/severity")]
    public IActionResult UpdateSeverity(Guid id) => StatusCode(501, "Not implemented");
}
