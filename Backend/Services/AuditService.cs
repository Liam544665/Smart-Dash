using SmartDash.Models;

namespace SmartDash.Services;

public class AuditService
{
    private readonly List<AuditLog> _logs = new();
    private const int MaxLogsInMemory = 1000;
    
    public void Log(string username, string action, string details, 
        string ipAddress, string userAgent, bool success)
    {
        var log = new AuditLog
        {
            Id = _logs.Count + 1,
            Timestamp = DateTime.UtcNow,
            Username = username,
            Action = action,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Success = success
        };
        
        _logs.Add(log);
        
        // Keep only recent logs in memory
        if (_logs.Count > MaxLogsInMemory)
        {
            _logs.RemoveAt(0);
        }
        
        // Log to console (in production: write to file/database)
        var status = success ? "SUCCESS" : "FAILED";
        var color = success ? Console.ForegroundColor : ConsoleColor.Red;
        
        Console.ForegroundColor = color;
        Console.WriteLine($"[AUDIT {status}] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} | " +
                         $"{username} | {action} | {details} | IP: {ipAddress}");
        Console.ResetColor();
    }
    
    public List<AuditLog> GetRecentLogs(int count = 100)
    {
        return _logs.OrderByDescending(l => l.Timestamp).Take(count).ToList();
    }
    
    public List<AuditLog> GetLogsByUsername(string username, int count = 50)
    {
        return _logs
            .Where(l => l.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToList();
    }
    
    public List<AuditLog> GetFailedLogins(int count = 50)
    {
        return _logs
            .Where(l => l.Action.Contains("Login") && !l.Success)
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToList();
    }
    
    public Dictionary<string, int> GetSecurityStats()
    {
        return new Dictionary<string, int>
        {
            ["TotalLogs"] = _logs.Count,
            ["FailedLogins"] = _logs.Count(l => l.Action.Contains("Login") && !l.Success),
            ["SuccessfulLogins"] = _logs.Count(l => l.Action == "LoginSuccess"),
            ["AccountLockouts"] = _logs.Count(l => l.Action == "AccountLocked"),
            ["Last24Hours"] = _logs.Count(l => l.Timestamp > DateTime.UtcNow.AddHours(-24))
        };
    }
}
