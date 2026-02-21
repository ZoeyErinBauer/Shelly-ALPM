namespace Shelly_Notifications.Models;

public class ShellyConfig
{
    public bool TrayEnabled { get; set; } = true;

    public int TrayCheckIntervalHours { get; set; } = 12;
}