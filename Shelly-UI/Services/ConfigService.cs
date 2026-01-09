using System;
using System.IO;
using System.Text.Json;
using Shelly_UI.Models;

public class ConfigService
{
    //home/user/.local/share/Shelly
    private static readonly string ConfigFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "Shelly");
    
    private static readonly string ConfigPath = Path.Combine(ConfigFolder, "settings.json");

    public void SaveConfig(ShellyConfig config)
    {
        if (!Directory.Exists(ConfigFolder)) Directory.CreateDirectory(ConfigFolder);
        
        var json = JsonSerializer.Serialize(config);
        File.WriteAllText(ConfigPath, json);
    }

    public ShellyConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath)) return new ShellyConfig(); 

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<ShellyConfig>(json) ?? new ShellyConfig();
        }
        catch
        {
            return new ShellyConfig();
        }
    }
}