using System.Diagnostics;

namespace Shelly_Notifications.Services;

public static class AppRunner
{
    public static void LaunchAppIfNotRunning(string args)
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

        var existing = Process.GetProcessesByName(appName);
        if (existing.Length > 0)
        {
            Console.WriteLine($"[Shell-Notifications][AppRunner] {appName} already running");
            return;
        }

        Console.WriteLine($"[Shell-Notifications][AppRunner] Launching {targetPath}");
        var psi = new ProcessStartInfo
        {
            FileName = targetPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        
        string[] envKeys =
        [
            "HOME", "USER", "DISPLAY", "WAYLAND_DISPLAY",
            "XDG_RUNTIME_DIR", "XDG_CURRENT_DESKTOP", "XDG_SESSION_TYPE",
            "XDG_DATA_DIRS", "XDG_CONFIG_DIRS", "XDG_CONFIG_HOME",
            "DBUS_SESSION_BUS_ADDRESS",
            "GTK_THEME", "GTK_APPLICATION_PREFER_DARK_THEME",
            "GSETTINGS_BACKEND", "DCONF_PROFILE",
        ];

        foreach (var key in envKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(value))
                psi.Environment[key] = value;
        }

        Process.Start(psi);
    }

    public static async Task SpawnTerminalWithCommandAsync(string command)
    {
        var terminal = GetTerminal();

        if (terminal == null)
        {
            Console.WriteLine("[Shell-Notifications] No supported terminal emulator found.");
            return;
        }

        Console.WriteLine($"[Shell-Notifications] Spawning terminal {terminal} with command: {command}");

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = terminal,
            Arguments = $"-e bash -c \"{command}; echo; read -p 'Press Enter to close...'\"",
            UseShellExecute = false,
        });

        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    private static string? GetTerminal()
    {
        var envTerminal = Environment.GetEnvironmentVariable("TERMINAL");
        if (!string.IsNullOrEmpty(envTerminal) && IsCommandAvailable(envTerminal))
        {
            return envTerminal;
        }

        string[] terminals =
        [
            "alacritty",
            "rio",
            "ghostty",
            "kitty",
            "konsole",
            "kgx",
            "gnome-terminal",
            "xfce4-terminal",
            "lxterminal",
            "xterm",
            "st",
            "foot",
            "terminator"
        ];

        return terminals.FirstOrDefault(IsCommandAvailable);
    }

    private static bool IsCommandAvailable(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return false;

        return path.Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, command))
            .Any(File.Exists);
    }
}