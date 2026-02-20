namespace Shelly_Notifications.UpdateCheckService;

public class AppRunner
{
    public void LaunchAppIfNotRunning()
    {
        const string appName = "shelly-ui";
        const string optPath = "/opt/shelly/Shelly-UI";
        const string appPath = "/usr/shelly/Shelly-UI";

        //TODO: Figure out if opt or usr is installed opt has prio for dev testing...
        //TODO: Is this spawning as a child or a new process... 
        
        var existing = System.Diagnostics.Process.GetProcessesByName(appName);
        if (existing.Length > 0)
        {
            Console.WriteLine($"[SNI] {appName} already running");
            return;
        }

        Console.WriteLine($"[SNI] Launching {optPath}");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = optPath,
            UseShellExecute = false,
        });
    }
}