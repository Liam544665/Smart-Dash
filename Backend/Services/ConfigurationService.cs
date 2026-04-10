using SmartDash.Models;
using System.Text.Json;

namespace SmartDash.Services;

public class ConfigurationService
{
    private const string ConfigFile = "config.json";
    private SystemConfig _config;

    public ConfigurationService()
    {
        _config = LoadConfig();
    }

    private SystemConfig LoadConfig()
    {
        if (File.Exists(ConfigFile))
        {
            try
            {
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<SystemConfig>(json) ?? new SystemConfig();
            }
            catch
            {
                return new SystemConfig();
            }
        }

        var defaultConfig = new SystemConfig();
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    private void SaveConfig(SystemConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(ConfigFile, json);
    }

    public SystemConfig GetConfig()
    {
        return _config;
    }

    public void UpdateDeviceMode(string device, string mode)
    {
        switch (device.ToLower())
        {
            case "aranet":
                _config.Aranet.Mode = mode;
                break;
            case "plug":
                _config.Plug.Mode = mode;
                break;
            case "camera":
                _config.Camera.Mode = mode;
                break;
        }
        SaveConfig(_config);
    }

    public void UpdateCameraUrl(string url)
    {
        _config.Camera.StreamUrl = url;
        SaveConfig(_config);
    }
}
