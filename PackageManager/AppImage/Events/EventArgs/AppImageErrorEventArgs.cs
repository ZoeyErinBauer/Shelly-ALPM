namespace PackageManager.AppImage.Events.EventArgs;

public class AppImageErrorEventArgs(string error) : System.EventArgs
{
    public string Error { get; } = error;
}
