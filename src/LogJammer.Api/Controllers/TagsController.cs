using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => StatusCode(501, "Not implemented");

    [HttpPost]
    public IActionResult Create() => StatusCode(501, "Not implemented");

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id) => StatusCode(501, "Not implemented");

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id) => StatusCode(501, "Not implemented");
}
