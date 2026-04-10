using SmartDash.Models;

namespace SmartDash.Services;

public class CameraService
{
    // Mode locked to simulated for Honours project demo
    private readonly string _mode = "simulated";
    private readonly Random _random = new();

    public void SetMode(string mode)
    {
        // Intentionally no-op - locked to simulated mode
        // Live streaming removed in favor of WebRTC future implementation
    }

    public void SetStreamUrl(string url)
    {
        // Intentionally no-op - simulated mode doesn't use stream URLs
    }

    public CameraSnapshot GetSnapshot()
    {
        // Always return simulated mode with random video
        var videoUrl = GetRandomVideo();
        
        return new CameraSnapshot
        {
            StreamUrl = videoUrl,
            IsLive = false,
            Timestamp = DateTime.UtcNow
        };
    }

    private string GetRandomVideo()
    {
        try
        {
            // Get path to videos folder
            var videosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "videos");
            
            // Get all .mp4 files
            var videoFiles = Directory.GetFiles(videosPath, "*.mp4");
            
            if (videoFiles.Length == 0)
            {
                // Fallback if no videos found
                Console.WriteLine("⚠️ No videos found in /wwwroot/videos/");
                return "/videos/placeholder.mp4";
            }
            
            // Pick random video
            var randomVideo = videoFiles[_random.Next(videoFiles.Length)];
            
            // Convert to web path
            var fileName = Path.GetFileName(randomVideo);
            var webPath = $"/videos/{Uri.EscapeDataString(fileName)}";
            
            Console.WriteLine($"🎥 Selected video: {fileName}");
            return webPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error selecting random video: {ex.Message}");
            return "/videos/placeholder.mp4";
        }
    }
}
