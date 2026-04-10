using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDash.Services;

namespace SmartDash.Controllers;

[Authorize] // Requires authentication
[ApiController]
[Route("api/[controller]")]
public class CameraController : ControllerBase
{
    private readonly CameraService _cameraService;

    public CameraController(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    [HttpGet("snapshot")]
    public IActionResult GetSnapshot()
    {
        return Ok(_cameraService.GetSnapshot());
    }
}
