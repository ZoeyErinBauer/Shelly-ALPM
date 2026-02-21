using System.Text.Json;
using Shelly_Notifications.Models;

namespace Shelly_Notifications.UpdateCheckService;

public class ConfigReader
{
    private static readonly string ConfigFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Shelly");

    private static readonly string ConfigPath = Path.Combine(ConfigFolder, "settings.json");

    private ShellyConfig? _config = null;
    
    public void Refresh()
    {
        _config = null;
    }
    
    public ShellyConfig LoadConfig()
    {
        try
        {
            if (_config != null)
            {
                return _config;
            }

            if (!File.Exists(ConfigPath)) return new ShellyConfig();
            var json = File.ReadAllText(ConfigPath);
            Console.WriteLine(ConfigPath);
            _config = JsonSerializer.Deserialize(json, NotificationJsonContext.Default.ShellyConfig) ?? new ShellyConfig();
            return _config;
        }
        catch
        {
            return new ShellyConfig();
        }
    }
}