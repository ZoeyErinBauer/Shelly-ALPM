using System.Diagnostics;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class AppImageInstallCommand : AsyncCommand<AppImageInstallSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppImageInstallSettings settings)
    {
        if (settings.PackageLocation == null)
        {
            if (Program.IsUiMode)
            {
                await Console.Error.WriteLineAsync("Error: No package specified");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error: No package specified[/]");
            }

            return 1;
        }

        if (!File.Exists(settings.PackageLocation))
        {
            if (Program.IsUiMode)
            {
                await Console.Error.WriteLineAsync("Error: Specified file does not exist.");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error: Specified file does not exist.[/]");
            }

            return 1;
        }

        RootElevator.EnsureRootExectuion();
        if (await IsAppImage(settings.PackageLocation))
        {
            return await InstallAppImage(settings);
        }
        
        return 0;
    }

    private static Task<int> InstallAppImage(AppImageInstallSettings settings)
    {
        var filePath = settings.PackageLocation!;

        var installDir = Path.Combine("/opt/shelly");
        Directory.CreateDirectory(installDir);

        var destPath = Path.Combine(installDir, Path.GetFileName(filePath));
        File.Copy(filePath, destPath, overwrite: true);
        AnsiConsole.MarkupLine($"[green]Copied appimage to: {destPath.EscapeMarkup()}[/]");

        SetFilePermissions(destPath, "a+x");
        AnsiConsole.MarkupLine($"[green]Setting file permissions to: a+x[/]");

        var appName = Path.GetFileNameWithoutExtension(filePath);

        Console.WriteLine("Creating desktop entry...");
        CreateDesktopEntry(
            appName: appName,
            executablePath: destPath,
            comment: $"{appName} - Installed from {appName}",
            terminal: false,
            categories: "Utility;"
        );
        AnsiConsole.MarkupLine($"[green]Desktop Entries Created[/]");

        return Task.FromResult(0);
    }

    private static Task<bool> IsAppImage(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return Task.FromResult(extension == ".AppImage");
    }

    private static void SetFilePermissions(string filePath, string permissions)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{permissions} \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not set file permissions: {ex.Message}[/]");
        }
    }

    private static void CreateDesktopEntry(
        string appName,
        string executablePath,
        string? comment = null,
        string icon = "application-x-executable",
        bool terminal = false,
        string categories = "Utility;")
    {
        const string desktopDir = "/usr/share/applications";
        var cleanName = CleanInvalidNames(appName);
        var desktopFilePath = Path.Combine(desktopDir, $"{cleanName}.desktop");

        var content = new StringBuilder();
        content.AppendLine("[Desktop Entry]");
        content.AppendLine("Version=1.0");
        content.AppendLine("Type=Application");
        content.AppendLine($"Name={appName}");
        content.AppendLine($"Comment={comment ?? $"{appName} application"}");
        content.AppendLine($"Exec={executablePath}");
        content.AppendLine($"Icon={icon}");
        content.AppendLine($"Terminal={terminal.ToString().ToLower()}");
        content.AppendLine($"Categories={categories}");
        content.AppendLine("StartupNotify=true");

        try
        {
            Directory.CreateDirectory(desktopDir);
            File.WriteAllText(desktopFilePath, content.ToString());
            SetFilePermissions(desktopFilePath, "644");
            UpdateDesktopDatabase(desktopDir);

            AnsiConsole.MarkupLine($"[green]Desktop entry created: {desktopFilePath}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not create desktop entry: {ex.Message}[/]");
        }
    }

    private static string CleanInvalidNames(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace("/", "-")
            .Replace("\\", "-");
    }

    private static void UpdateDesktopDatabase(string desktopDir)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "update-desktop-database",
                Arguments = $"\"{desktopDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not set desktop database: {ex.Message}[/]");
        }
    }
}