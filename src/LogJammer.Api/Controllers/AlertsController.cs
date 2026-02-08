using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => StatusCode(501, "Not implemented");

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id) => StatusCode(501, "Not implemented");

    [HttpPost("{id:guid}/acknowledge")]
    public IActionResult Acknowledge(Guid id) => StatusCode(501, "Not implemented");
}
