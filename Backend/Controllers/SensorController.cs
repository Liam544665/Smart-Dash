using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDash.Services;
using SmartDash.Models;

namespace SmartDash.Controllers;

[Authorize] // Requires authentication
[ApiController]
[Route("api/[controller]")]
public class SensorController : ControllerBase
{
    private readonly SensorService _sensorService;

    public SensorController(SensorService sensorService)
    {
        _sensorService = sensorService;
    }

    [HttpGet("readings")]
    public IActionResult GetReadings()
    {
        return Ok(_sensorService.GetReadings());
    }

    // ============================================================================
    // BLUETOOTH ENDPOINTS - ARANET4 INTEGRATION
    // ============================================================================

    [HttpGet("bluetooth/scan")]
    public async Task<IActionResult> ScanForDevices()
    {
        try
        {
            var devices = await _sensorService.ScanForAranetDevicesAsync();
            return Ok(new 
            { 
                success = true, 
                count = devices.Count,
                devices = devices 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new 
            { 
                success = false, 
                error = ex.Message 
            });
        }
    }

    [HttpPost("bluetooth/connect")]
    public async Task<IActionResult> ConnectDevice([FromBody] BluetoothConnectionRequest request)
    {
        try
        {
            var success = await _sensorService.ConnectToAranetAsync(request.DeviceAddress);
            
            if (success)
            {
                return Ok(new 
                { 
                    success = true, 
                    message = "Connected to Aranet4 device",
                    status = _sensorService.GetBluetoothStatus()
                });
            }
            else
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "Failed to connect to device" 
                });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new 
            { 
                success = false, 
                error = ex.Message 
            });
        }
    }

    [HttpPost("bluetooth/disconnect")]
    public async Task<IActionResult> DisconnectDevice()
    {
        try
        {
            await _sensorService.DisconnectAranetAsync();
            return Ok(new 
            { 
                success = true, 
                message = "Disconnected from Aranet4 device" 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new 
            { 
                success = false, 
                error = ex.Message 
            });
        }
    }

    [HttpGet("bluetooth/status")]
    public IActionResult GetBluetoothStatus()
    {
        try
        {
            var status = _sensorService.GetBluetoothStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            return BadRequest(new 
            { 
                success = false, 
                error = ex.Message 
            });
        }
    }
}
