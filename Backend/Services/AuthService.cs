using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartDash.Models;
using BCrypt.Net;

namespace SmartDash.Services;

public class AuthService
{
    private readonly IConfiguration _config;
    private readonly AuditService _auditService;
    private readonly List<User> _users; // In production: use database (EF Core)
    
    // Security settings
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int TokenExpirationHours = 8;
    
    public AuthService(IConfiguration config, AuditService auditService)
    {
        _config = config;
        _auditService = auditService;
        _users = new List<User>
        {
            new User 
            { 
                Id = 1,
                Username = "admin",
                Email = "admin@smartdash.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), // MUST CHANGE
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User 
            { 
                Id = 2,
                Username = "user",
                Email = "user@smartdash.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"), // MUST CHANGE
                Role = "User",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };
    }
    
    public LoginResponse? Login(LoginRequest request, string ipAddress, string userAgent)
    {
        // Find user
        var user = _users.FirstOrDefault(u => 
            u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase));
        
        if (user == null)
        {
            _auditService.Log("Unknown", "LoginFailed", 
                $"Username not found: {request.Username}", ipAddress, userAgent, false);
            return null;
        }
        
        // Check if account is locked
        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            var remainingMinutes = (int)(user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes;
            _auditService.Log(user.Username, "LoginBlocked", 
                $"Account locked for {remainingMinutes} more minutes", ipAddress, userAgent, false);
            return null;
        }
        
        // Check if account is active
        if (!user.IsActive)
        {
            _auditService.Log(user.Username, "LoginFailed", 
                "Account is inactive", ipAddress, userAgent, false);
            return null;
        }
        
        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // Increment failed attempts
            user.FailedLoginAttempts++;
            
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _auditService.Log(user.Username, "AccountLocked", 
                    $"Too many failed attempts. Locked for {LockoutMinutes} minutes", 
                    ipAddress, userAgent, false);
            }
            else
            {
                _auditService.Log(user.Username, "LoginFailed", 
                    $"Invalid password. Attempt {user.FailedLoginAttempts}/{MaxFailedAttempts}", 
                    ipAddress, userAgent, false);
            }
            
            return null;
        }
        
        // Reset failed attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        user.LastLogin = DateTime.UtcNow;
        
        // Generate JWT token
        var token = GenerateJwtToken(user);
        
        _auditService.Log(user.Username, "LoginSuccess", 
            "User logged in successfully", ipAddress, userAgent, true);
        
        return new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddHours(TokenExpirationHours)
        };
    }
    
    public bool Register(RegisterRequest request, string ipAddress, string userAgent)
    {
        // Check if username exists
        if (_users.Any(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
        {
            _auditService.Log(request.Username, "RegisterFailed", 
                "Username already exists", ipAddress, userAgent, false);
            return false;
        }
        
        // Check if email exists
        if (_users.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
        {
            _auditService.Log(request.Username, "RegisterFailed", 
                "Email already exists", ipAddress, userAgent, false);
            return false;
        }
        
        // Create new user
        var newUser = new User
        {
            Id = _users.Max(u => u.Id) + 1,
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "User", // Default role
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        _users.Add(newUser);
        
        _auditService.Log(newUser.Username, "UserRegistered", 
            "New user account created", ipAddress, userAgent, true);
        
        return true;
    }
    
    private string GenerateJwtToken(User user)
    {
        var secretKey = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        
        if (secretKey.Length < 32)
            throw new InvalidOperationException("JWT Secret must be at least 32 characters");
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("LoginTime", DateTime.UtcNow.ToString("o")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Unique token ID
        };
        
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "SmartDash",
            audience: _config["Jwt:Audience"] ?? "SmartDashAPI",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(TokenExpirationHours),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public bool ValidateToken(string token)
    {
        try
        {
            var secretKey = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
            }, out _);
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public User? GetUserByUsername(string username)
    {
        return _users.FirstOrDefault(u => 
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }
}
