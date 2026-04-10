using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDash.Models;
using SmartDash.Services;

namespace SmartDash.Controllers;

[Authorize] // Requires authentication
[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ConfigurationService _configService;
    private readonly SmartPlugService _plugService;
    private readonly SensorService _sensorService;
    private readonly CameraService _cameraService;
    private readonly AuditService _auditService;

    public ConfigController(
        ConfigurationService configService,
        SmartPlugService plugService,
        SensorService sensorService,
        CameraService cameraService,
        AuditService auditService)
    {
        _configService = configService;
        _plugService = plugService;
        _sensorService = sensorService;
        _cameraService = cameraService;
        _auditService = auditService;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        return Ok(_configService.GetConfig());
    }

    [Authorize(Roles = "Admin")] // Only admins can change device modes
    [HttpPost("device-mode")]
    public IActionResult UpdateDeviceMode([FromBody] DeviceModeUpdateRequest request)
    {
        var username = User.Identity?.Name ?? "Unknown";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        
        // Update the specific device mode
        switch (request.Device.ToLower())
        {
            case "aranet":
                _configService.UpdateDeviceMode("aranet", request.Mode);
                _sensorService.SetMode(request.Mode);
                break;
            case "plug":
                _configService.UpdateDeviceMode("plug", request.Mode);
                _plugService.SetMode(request.Mode);
                break;
            case "camera":
                _configService.UpdateDeviceMode("camera", request.Mode);
                _cameraService.SetMode(request.Mode);
                break;
            default:
                return BadRequest(new { error = "Invalid device name" });
        }

        _auditService.Log(username, "DeviceModeChange", 
            $"{request.Device} mode changed to {request.Mode}", ipAddress, userAgent, true);

        return Ok(new { success = true, device = request.Device, mode = request.Mode });
    }

    [Authorize(Roles = "Admin")] // Only admins can change camera URL
    [HttpPost("camera-url")]
    public IActionResult UpdateCameraUrl([FromBody] CameraUrlUpdateRequest request)
    {
        var username = User.Identity?.Name ?? "Unknown";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        
        _configService.UpdateCameraUrl(request.Url);
        _cameraService.SetStreamUrl(request.Url);

        _auditService.Log(username, "CameraUrlChange", 
            $"Camera stream URL updated", ipAddress, userAgent, true);

        return Ok(new { success = true, url = request.Url });
    }
}

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ConfigurationService _configService;

    public HealthController(ConfigurationService configService)
    {
        _configService = configService;
    }

    [AllowAnonymous] // Health check doesn't require auth
    [HttpGet]
    public IActionResult GetHealth()
    {
        var config = _configService.GetConfig();
        return Ok(new
        {
            status = "ok",
            aranetMode = config.Aranet.Mode,
            plugMode = config.Plug.Mode,
            cameraMode = config.Camera.Mode,
            timestamp = DateTime.UtcNow,
            secure = true
        });
    }
}
