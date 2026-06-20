using Microsoft.AspNetCore.Mvc;

namespace DocIntel.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        service = "DocIntel API",
        timeUtc = DateTime.UtcNow
    });
}
