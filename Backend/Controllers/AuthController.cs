using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDash.Models;
using SmartDash.Services;
using SmartDash.Validators;

namespace SmartDash.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AuditService _auditService;
    
    public AuthController(AuthService authService, AuditService auditService)
    {
        _authService = authService;
        _auditService = auditService;
    }
    
    /// <summary>
    /// Login endpoint - returns JWT token
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Validate input
        var validator = new LoginValidator();
        var validationResult = validator.Validate(request);
        
        if (!validationResult.IsValid)
        {
            return BadRequest(new 
            { 
                message = "Validation failed",
                errors = validationResult.Errors.Select(e => e.ErrorMessage) 
            });
        }
        
        // Get client info
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        
        // Attempt login
        var response = _authService.Login(request, ipAddress, userAgent);
        
        if (response == null)
        {
            return Unauthorized(new 
            { 
                message = "Invalid credentials or account locked. " +
                         "Your account will be locked after 5 failed attempts for 15 minutes."
            });
        }
        
        return Ok(response);
    }
    
    /// <summary>
    /// Register new user account
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        // Validate input
        var validator = new RegisterValidator();
        var validationResult = validator.Validate(request);
        
        if (!validationResult.IsValid)
        {
            return BadRequest(new 
            { 
                message = "Validation failed",
                errors = validationResult.Errors.Select(e => e.ErrorMessage) 
            });
        }
        
        // Get client info
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        
        // Attempt registration
        var success = _authService.Register(request, ipAddress, userAgent);
        
        if (!success)
        {
            return BadRequest(new 
            { 
                message = "Username or email already exists" 
            });
        }
        
        return Created("/api/auth/login", new 
        { 
            message = "Account created successfully. Please login." 
        });
    }
    
    /// <summary>
    /// Logout endpoint - invalidate token (client-side)
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var username = User.Identity?.Name ?? "Unknown";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        
        _auditService.Log(username, "Logout", "User logged out", ipAddress, userAgent, true);
        
        // Note: JWT tokens can't be invalidated server-side without a blacklist
        // Client should delete the token from storage
        return Ok(new { message = "Logged out successfully" });
    }
    
    /// <summary>
    /// Get current user info (requires authentication)
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();
        
        var user = _authService.GetUserByUsername(username);
        if (user == null)
            return NotFound();
        
        return Ok(new 
        {
            user.Username,
            user.Email,
            user.Role,
            user.CreatedAt,
            user.LastLogin
        });
    }
    
    /// <summary>
    /// Get audit logs (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("audit-logs")]
    public IActionResult GetAuditLogs([FromQuery] int count = 100)
    {
        var logs = _auditService.GetRecentLogs(count);
        return Ok(logs);
    }
    
    /// <summary>
    /// Get security statistics (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("security-stats")]
    public IActionResult GetSecurityStats()
    {
        var stats = _auditService.GetSecurityStats();
        return Ok(stats);
    }
}
