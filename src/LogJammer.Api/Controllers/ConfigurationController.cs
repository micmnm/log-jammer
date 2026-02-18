using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ConfigurationController(IConfigurationService configService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConfigurationResponse>>> Get(CancellationToken cancellationToken = default)
    {
        var configs = await configService.GetAllAsync(cancellationToken);
        return Ok(configs);
    }

    [HttpPut]
    public async Task<ActionResult<ConfigurationResponse>> Update([FromBody] UpdateConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        var config = await configService.UpdateAsync(request, cancellationToken);
        return Ok(config);
    }
}
