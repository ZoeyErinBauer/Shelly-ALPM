namespace Shelly_Notifications.Models;

public class ShellyConfig
{
    public bool TrayEnabled { get; set; } = true;

    public int TrayCheckIntervalHours { get; set; } = 12;
    
    public bool UseWeeklySchedule { get; set; } = false;
    
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];
    
    public TimeOnly? Time { get; set; } = null;
}