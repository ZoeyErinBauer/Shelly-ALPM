using Avalonia;
using ReactiveUI.Avalonia;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Shelly.Utilities.System;

namespace Shelly_UI;

sealed class Program
{
    private static bool _crashed = false;
    private const string PipeName = "ShellyUI_ActivationPipe";
    private static CancellationTokenSource? _pipeCancellation;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MANGOHUD")))
        {
            Environment.SetEnvironmentVariable("MANGOHUD", "0");
        }

        // Start listening for activation signals from other instances
        _pipeCancellation = new CancellationTokenSource();
        Task.Run(() => ListenForActivationSignals(_pipeCancellation.Token));

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
            else
            {
                Console.Error.WriteLine("Shelly-UI crashed. Check log file for details.");
            }

            // Cleanup
            _pipeCancellation?.Cancel();
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

    private static async Task ListenForActivationSignals(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server);
                var message = await reader.ReadLineAsync();

                if (message == "ACTIVATE")
                {
                    // Signal the App to show the window
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (App.Current is App app)
                        {
                            app.ShowWindowCommand.Execute(null);
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in activation listener: {ex.Message}");
                await Task.Delay(100, cancellationToken);
            }
        }
    }
}