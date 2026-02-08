using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => StatusCode(501, "Not implemented");

    [HttpPut]
    public IActionResult Update() => StatusCode(501, "Not implemented");
}
