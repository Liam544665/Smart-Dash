using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using SmartDash.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// SECURITY CONFIGURATION
// ============================================================================

// 1. JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? throw new InvalidOperationException("JWT Secret not configured in appsettings.json");

if (jwtSecret.Length < 32)
    throw new InvalidOperationException("JWT Secret must be at least 32 characters for security");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SmartDash",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SmartDashAPI",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
        };
        
        // Handle authentication errors
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"Authentication challenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Define authorization policies
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User", "Admin"));
});

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "https://localhost:5173",
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "null" // Allow file:// origins for testing HTML
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Allow cookies for session management
        });
});

// ============================================================================
// APPLICATION SERVICES
// ============================================================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Smart Dash API", 
        Version = "v1",
        Description = "Secure Smart Home Dashboard API"
    });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Register application services
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SmartPlugService>();
builder.Services.AddSingleton<SensorService>();
builder.Services.AddSingleton<CameraService>();
builder.Services.AddSingleton<ConfigurationService>();

// ============================================================================
// EXPERIMENTAL: WebRTC Streaming (Post-Graduation Implementation)
// ============================================================================
// Future implementation plan:
// - Replace FFmpeg/HLS approach with WebRTC for <1s latency
// - Use go2rtc or custom WebRTC signaling server
// - Implement STUN/TURN servers for NAT traversal
// - Add ICE candidate exchange via SignalR
// Status: Research complete, implementation deferred
// See: /Docs/WEBRTC_RESEARCH.md for technical details
// ============================================================================

// ============================================================================
// BUILD APPLICATION
// ============================================================================

var app = builder.Build();

// Initialize services with config values
var configService = app.Services.GetRequiredService<ConfigurationService>();
var cameraService = app.Services.GetRequiredService<CameraService>();
var config = configService.GetConfig();

// Load camera stream URL from config
cameraService.SetStreamUrl(config.Camera.StreamUrl);
cameraService.SetMode(config.Camera.Mode);

// ============================================================================
// MIDDLEWARE PIPELINE (ORDER MATTERS!)
// ============================================================================

// 1. Static Files (for embedded UI)
app.UseDefaultFiles(); // Serves index.html by default
app.UseStaticFiles();

// 2. HTTPS Redirection
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // HTTP Strict Transport Security
}
app.UseHttpsRedirection();

// 3. Swagger (Development only for security)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Dash API v1");
    });
}

// 4. CORS
app.UseCors("AllowReactApp");

// 5. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 6. Controllers
app.MapControllers();

// ============================================================================
// STARTUP BANNER
// ============================================================================

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("SMART DASH BACKEND - SECURE MODE");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"Backend: ASP.NET Core {Environment.Version}");
Console.WriteLine($"Running on: https://localhost:5001");
if (app.Environment.IsDevelopment())
{
    Console.WriteLine($"Swagger UI: https://localhost:5001/swagger");
}
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("SECURITY FEATURES ENABLED:");
Console.WriteLine("  ✅ JWT Authentication");
Console.WriteLine("  ✅ Role-Based Authorization (Admin/User)");
Console.WriteLine("  ✅ HTTPS/TLS Encryption");
Console.WriteLine("  ✅ Input Validation");
Console.WriteLine("  ✅ Audit Logging");
Console.WriteLine("  ✅ Account Lockout (5 failed attempts = 15 min lockout)");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("DEFAULT CREDENTIALS (CHANGE IMMEDIATELY):");
Console.WriteLine("  Admin: username='admin', password='Admin@123'");
Console.WriteLine("  User:  username='user',  password='User@123'");
Console.WriteLine("=".PadRight(70, '='));

// ============================================================================
// AUTO-OPEN BROWSER
// ============================================================================

string url = "https://localhost:5001";

_ = Task.Run(async () =>
{
    // Wait a moment for the server to start
    await Task.Delay(1500);
    
    try
    {
        Console.WriteLine($"\n🌐 Opening browser at {url}...");
        OpenBrowser(url);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Could not auto-open browser: {ex.Message}");
        Console.WriteLine($"Please manually open: {url}");
    }
});

static void OpenBrowser(string url)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
    catch
    {
        // Fallback - sometimes the above doesn't work
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}

app.Run("https://localhost:5001");
