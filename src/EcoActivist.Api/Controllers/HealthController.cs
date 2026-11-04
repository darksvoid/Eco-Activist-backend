using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new { status = "Healthy", timestamp = DateTimeOffset.UtcNow });
    }
}
