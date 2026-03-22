
using Shelly.Gtk.Enums;

namespace Shelly.Gtk.UiModels;

public class ShellyConfig
{
    public string? AccentColor { get; set; }
    
    public string? Culture {get; set;}
    
    public bool DarkMode { get; set; } = true;

    public bool AurEnabled { get; set; } = false;
    
    public bool AurWarningConfirmed { get; set; } = false;
    
    public bool FlatPackEnabled { get; set; } = false;
    
    public bool ConsoleEnabled { get; set; } = false;
    
    public double WindowWidth { get; set; } = 800;
    
    public double WindowHeight { get; set; } = 600;
    
    public DefaultViewEnum DefaultView  { get; set; }
    
    public bool UseKdeTheme { get; set; } = false;
    
    public bool UseHorizontalMenu { get; set; } = true;

    public bool TrayEnabled { get; set; } = true;

    public int TrayCheckIntervalHours { get; set; } = 12;

    public bool NoConfirm { get; set; } = false;
    
    public bool NewInstall { get; set; } = true;
    
    public Version CurrentVersion { get; set; } = new Version(0,0,0);

    public bool UseWeeklySchedule { get; set; } = false;
    
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];
    
    public TimeOnly? Time { get; set; } = null;
    
    public bool WebViewEnabled { get; set; } = false;
}
