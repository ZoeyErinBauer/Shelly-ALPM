using Avalonia;
using ReactiveUI.Avalonia;
using System;
using System.IO;
using Shelly.Utilities.System;

namespace Shelly_UI;

sealed class Program
{
    private static bool _crashed = false;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine($"Running with user path {EnvironmentManager.UserPath}");
        
        // Log to user's Documents folder (no longer running as root)
        var logPath = Path.Combine(EnvironmentManager.UserPath, "Documents", "Shelly");
        Directory.CreateDirectory(logPath);
        var logWriter = new LogTextWriter(Console.Error, logPath);
        Console.SetError(logWriter);
        
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            _crashed = true;
            Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");
            Console.Error.WriteLine("Shelly-UI is shutting down due to unhandled exception.");
            Environment.Exit(1);
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            if (!_crashed)
            {
                logWriter.DeleteLog();
            }
            else
            {
                Console.Error.WriteLine("Shelly-UI crashed. Check log file for details.");
            }
        };

        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("Shelly-UI is exclusively for Arch Linux.");
            return;
        }

        // No longer need to elevate to root - the GUI communicates with the
        // privileged shelly-service via D-Bus for package operations.
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {

        var builder = AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (sessionType == "wayland")
        {
            // Force Wayland backend, no X11 fallback
            return builder
                .UseSkia()
                .With(new AvaloniaNativePlatformOptions())
                .UsePlatformDetect(); // Will now prefer Wayland
        }
    
        return builder.UsePlatformDetect();
    }
}
