namespace Shelly_Notifications.UpdateCheckService;

public class AppRunner
{
    public void LaunchAppIfNotRunning()
    {
        const string appName = "shelly-ui";
        const string optPath = "/opt/shelly/Shelly-UI";
        const string appPath = "/usr/bin/shelly-ui";

        string targetPath;
        if (File.Exists(appPath))
        {
            targetPath = appPath;
        }
        else if (File.Exists(optPath))
        {
            targetPath = optPath;
        }
        else
        {
            Console.WriteLine($"[Shell-Notifications][AppRunner] {appName} not found in {optPath} or {appPath}");
            return;
        }
        
        var existing = System.Diagnostics.Process.GetProcessesByName(appName);
        if (existing.Length > 0)
        {
            Console.WriteLine($"[Shell-Notifications][AppRunner] {appName} already running");
            return;
        }

        Console.WriteLine($"[Shell-Notifications][AppRunner] Launching {targetPath}");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}