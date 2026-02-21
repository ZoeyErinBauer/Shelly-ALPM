using System;
using System.Diagnostics;
using System.IO;

namespace Shelly_UI.Services.TrayServices;

public static class TrayStartService
{
    public static void Start()
    {
        try
        {
            const string appPath = "/usr/shelly/Shelly-UI";
            const string optPath = "/opt/shelly/Shelly-Notifications";
            var path = "";

            if (File.Exists(appPath))
            {
                path = appPath;
            }

            if (File.Exists(optPath))
            {
                path = optPath;
            }

            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"Tray service executable not found at: {optPath}");
                Console.WriteLine($"Tray service executable not found at: {appPath}");
                return;
            }

            try
            {
                var running = Process.GetProcessesByName("Shelly-Notifications").Length > 0;
                if (running)
                {
                    Console.WriteLine("Tray service is already running (process detected).");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to check running processes: {ex.Message}");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();
            process.Dispose(); // Detach from parent process
            Console.WriteLine("Tray service started successfully as detached process.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start tray service: {ex.Message}");
        }
    }
}