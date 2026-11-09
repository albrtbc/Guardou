using Guardou.Application.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardou.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/config")]
public sealed class ConfigurationController(RuntimeConfigurationQuery query) : ControllerBase
{
    [HttpGet(Name = "GetConfiguration")]
    [ProducesResponseType<RuntimeConfiguration>(StatusCodes.Status200OK)]
    public Task<RuntimeConfiguration> Get(CancellationToken cancellationToken)
    {
        return query.GetAsync(cancellationToken);
    }
}
