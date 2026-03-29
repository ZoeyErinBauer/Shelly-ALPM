namespace Shelly.Gtk.UiModels;

public class OperationLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public bool IsSudo { get; set; }
    public int? ExitCode { get; set; }
}
