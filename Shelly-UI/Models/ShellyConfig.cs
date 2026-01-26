using Avalonia.Controls;

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
    
    public double WindowWidth { get; set; } = 800;
    
    public double WindowHeight { get; set; } = 600;
    
    public WindowState WindowState { get; set; } = WindowState.Normal;
}