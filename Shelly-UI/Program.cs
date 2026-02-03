using Avalonia;
using ReactiveUI.Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PackageManager.User;
using Shelly_UI.Enums;
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
        var logPath = Path.Combine(EnvironmentManager.UserPath, ".config", "shelly","logs");
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
                .With(new X11PlatformOptions()
                {
                    //This option should allow for native scaling support
                    EnableIme = true,
                    EnableMultiTouch = true,
                    UseDBusMenu = true,
                    UseDBusFilePicker = true
                })
                .With(new AvaloniaNativePlatformOptions())
                .UsePlatformDetect(); 
        }
    
        return builder.UsePlatformDetect();
    }
}