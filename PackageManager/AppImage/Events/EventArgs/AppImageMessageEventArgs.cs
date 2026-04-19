namespace PackageManager.AppImage.Events.EventArgs;

public class AppImageMessageEventArgs(string message) : System.EventArgs
{
    public string Message { get; } = message;
}
