using Avalonia;
using ReactiveUI.Avalonia;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using Shelly.Utilities.System;

namespace Shelly_UI;

sealed class Program
{
    private static bool _crashed = false;
    private static CancellationTokenSource? _pipeCancellation;
    private static FileStream? _lockFileStream;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("Shelly-UI is exclusively for Arch Linux.");
            return;
        }
        // Single instance lock
        try
        {
            var lockPath = Path.Combine(EnvironmentManager.UserPath, ".config", "shelly", "ui.lock");
            var lockDir = Path.GetDirectoryName(lockPath);
            if (!string.IsNullOrEmpty(lockDir) && !Directory.Exists(lockDir))
            {
                Directory.CreateDirectory(lockDir);
            }
            
            _lockFileStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            Console.WriteLine("Another instance of Shelly-UI is already running. Exiting...");
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to acquire lock: {ex.Message}");
        }
        
        
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MANGOHUD")))
        {
            Environment.SetEnvironmentVariable("MANGOHUD", "0");
        }

        Console.WriteLine($"Running with user path {EnvironmentManager.UserPath}");
        var logPath = Path.Combine(EnvironmentManager.UserPath, ".config", "shelly", "logs");
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

            // Cleanup
            _pipeCancellation?.Cancel();
            _lockFileStream?.Dispose();
        };

        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("Shelly-UI is exclusively for Arch Linux.");
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI()
            .With(new X11PlatformOptions()
            {
                //This option should allow for native scaling support
                EnableIme = true,
                EnableMultiTouch = true,
                UseDBusMenu = true,
                UseDBusFilePicker = true,
                RenderingMode =
                [
                    X11RenderingMode.Glx, X11RenderingMode.Vulkan, X11RenderingMode.Egl, X11RenderingMode.Software
                ],
            })
            .UsePlatformDetect();
    }
}