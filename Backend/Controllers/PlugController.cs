using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDash.Services;

namespace SmartDash.Controllers;

[Authorize] // Requires authentication
[ApiController]
[Route("api/[controller]")]
public class PlugController : ControllerBase
{
    private readonly SmartPlugService _plugService;

    public PlugController(SmartPlugService plugService)
    {
        _plugService = plugService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_plugService.GetStatus());
    }

    [HttpPost("toggle")]
    public IActionResult Toggle()
    {
        return Ok(_plugService.Toggle());
    }
}
