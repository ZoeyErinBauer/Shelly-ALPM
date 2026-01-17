namespace Shelly_UI.Models;

public class ShellyConfig
{
    public string? AccentColor { get; set; }
    
    public string? Culture {get; set;}
    
    public bool DarkMode { get; set; } = true;

    public bool AurEnabled { get; set; } = false;
    
    public bool FlatPackEnabled { get; set; } = false;
    
    public bool SnapEnabled { get; set; } = false;
    
    public bool ConsoleEnabled { get; set; } = false;
}