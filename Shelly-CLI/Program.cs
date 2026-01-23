using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PackageManager.Flatpak;

namespace Shelly_CLI;

public class DualOutputWriter : TextWriter
{
    private readonly TextWriter _primary;
    private readonly TextWriter _stderr;
    private const string ShellyPrefix = "[Shelly]";

    public DualOutputWriter(TextWriter primary, TextWriter stderr)
    {
        _primary = primary;
        _stderr = stderr;
    }

    public override void WriteLine(string? value)
    {
        _primary.WriteLine(value);
        // Also write to stderr with prefix for UI capture
        _stderr.WriteLine($"{ShellyPrefix}{value}");
    }

    public override void Write(string? value)
    {
        _primary.Write(value);
    }

    public override void Write(char value)
    {
        _primary.Write(value);
    }

    public override Encoding Encoding => _primary.Encoding;
}

public class StderrPrefixWriter : TextWriter
{
    private readonly TextWriter _stderr;
    private const string ShellyPrefix = "[Shelly]";

    public StderrPrefixWriter(TextWriter stderr)
    {
        _stderr = stderr;
    }

    public override void WriteLine(string? value)
    {
        _stderr.WriteLine($"{ShellyPrefix}{value}");
    }

    public override void Write(string? value)
    {
        _stderr.Write(value);
    }

    public override void Write(char value)
    {
        _stderr.Write(value);
    }

    public override Encoding Encoding => _stderr.Encoding;
}

/// <summary>
/// A TextWriter that filters out lines containing [bracketed] patterns when running in terminal mode.
/// </summary>
public class FilteringTextWriter : TextWriter
{
    private readonly TextWriter _inner;
    private static readonly Regex BracketedPattern = new Regex(@"\[[^\]]+\]", RegexOptions.Compiled);

    public FilteringTextWriter(TextWriter inner)
    {
        _inner = inner;
    }

    public override void WriteLine(string? value)
    {
        // Filter out lines that contain [somestring] patterns
        if (value != null && BracketedPattern.IsMatch(value))
        {
            return; // Skip this line
        }

        _inner.WriteLine(value);
    }

    public override void Write(string? value)
    {
        // For Write (without newline), pass through as-is since we filter on complete lines
        _inner.Write(value);
    }

    public override void Write(char value)
    {
        _inner.Write(value);
    }

    public override Encoding Encoding => _inner.Encoding;
}

public class Program
{
    public static int Main(string[] args)
    {
        // Check if running in UI mode (--ui-mode flag passed by Shelly-UI)
        var argsList = args.ToList();
        var isUiMode = argsList.Remove("--ui-mode");
        args = argsList.ToArray();

        if (isUiMode)
        {
            // Configure stderr to use prefix for UI integration
            var stderrWriter = new StderrPrefixWriter(Console.Error);
            Console.SetError(stderrWriter);

            // Configure AnsiConsole to use DualOutputWriter for UI integration
            var dualWriter = new DualOutputWriter(Console.Out, stderrWriter);
            Console.SetOut(dualWriter);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(dualWriter)
            });
        }
        else
        {
            // When running from terminal, filter out lines containing [bracketed] patterns
            var filteringStdout = new FilteringTextWriter(Console.Out);
            var filteringStderr = new FilteringTextWriter(Console.Error);
            Console.SetOut(filteringStdout);
            Console.SetError(filteringStderr);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(filteringStdout)
            });
        }

        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("shelly");
            config.SetApplicationVersion("1.2.2");

            config.AddCommand<SyncCommand>("sync")
                .WithDescription("Synchronize package databases");

            config.AddCommand<ListInstalledCommand>("list-installed")
                .WithDescription("List all installed packages");

            config.AddCommand<ListAvailableCommand>("list-available")
                .WithDescription("List all available packages");

            config.AddCommand<ListUpdatesCommand>("list-updates")
                .WithDescription("List packages that need updates");

            config.AddCommand<InstallCommand>("install")
                .WithDescription("Install one or more packages");

            config.AddCommand<RemoveCommand>("remove")
                .WithDescription("Remove one or more packages");

            config.AddCommand<UpdateCommand>("update")
                .WithDescription("Update one or more packages");

            config.AddCommand<UpgradeCommand>("upgrade")
                .WithDescription("Perform a full system upgrade");

            config.AddBranch("keyring", keyring =>
            {
                keyring.SetDescription("Manage pacman keyring");

                keyring.AddCommand<KeyringInitCommand>("init")
                    .WithDescription("Initialize the pacman keyring");

                keyring.AddCommand<KeyringPopulateCommand>("populate")
                    .WithDescription("Reload keys from keyrings in /usr/share/pacman/keyrings");

                keyring.AddCommand<KeyringRecvCommand>("recv")
                    .WithDescription("Receive keys from a keyserver");

                keyring.AddCommand<KeyringLsignCommand>("lsign")
                    .WithDescription("Locally sign the specified key(s)");

                keyring.AddCommand<KeyringListCommand>("list")
                    .WithDescription("List all keys in the keyring");

                keyring.AddCommand<KeyringRefreshCommand>("refresh")
                    .WithDescription("Refresh keys from the keyserver");
            });

            config.AddBranch("flatpak", flatpak =>
            {
                flatpak.SetDescription("Manage flatpak");

                flatpak.AddCommand<Flatpak.FlatpakInstallCommand>("install")
                    .WithDescription("Install flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakUpdateCommand>("update")
                    .WithDescription("Update flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakListCommand>("list")
                    .WithDescription("List installed flatpak apps");
                
                flatpak.AddCommand<Flatpak.FlatpakRunningCommand>("running")
                    .WithDescription("List running flatpak apps");

                /*flatpak.AddCommand<>("config")
                    .WithDescription("Config flatpak");*/

                flatpak.AddCommand<Flatpak.FlatpakRemoveCommand>("uninstall")
                    .WithDescription("Remove flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakRunCommand>("run")
                    .WithDescription("Run flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakKillCommand>("kill")
                    .WithDescription("Kill running flatpak app");

                flatpak.AddCommand<Flatpak.FlathubSearchCommand>("search")
                    .WithDescription("Search flatpak");
            });
        });

        return app.Run(args);
    }
}

public class SyncSettings : CommandSettings
{
    [CommandOption("-f|--force")]
    [Description("Force synchronization even if databases are up to date")]
    public bool Force { get; set; }
}

public class SyncCommand : Command<SyncSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] SyncSettings settings)
    {
        using var manager = new AlpmManager();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing ALPM...", ctx => { manager.Initialize(true); });

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Synchronizing package databases...", ctx => { manager.Sync(settings.Force); });

        AnsiConsole.MarkupLine("[green]Package databases synchronized successfully![/]");
        return 0;
    }
}

public class ListInstalledCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        using var manager = new AlpmManager();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing ALPM...", ctx => { manager.Initialize(true); });

        var packages = manager.GetInstalledPackages();

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Size");
        table.AddColumn("Description");

        foreach (var pkg in packages.OrderBy(p => p.Name))
        {
            table.AddRow(
                pkg.Name,
                pkg.Version,
                FormatSize(pkg.Size),
                pkg.Description.EscapeMarkup().Truncate(50)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Total: {packages.Count} packages[/]");
        return 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}

public class ListAvailableCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        using var manager = new AlpmManager();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing and syncing ALPM...", ctx => { manager.IntializeWithSync(); });

        var packages = manager.GetAvailablePackages();

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Repository");
        table.AddColumn("Description");

        foreach (var pkg in packages.OrderBy(p => p.Name).Take(100))
        {
            table.AddRow(
                pkg.Name,
                pkg.Version,
                pkg.Repository,
                pkg.Description.EscapeMarkup().Truncate(50)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Showing first 100 of {packages.Count} available packages[/]");
        return 0;
    }
}

public class ListUpdatesCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        using var manager = new AlpmManager();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing and syncing ALPM...", ctx => { manager.IntializeWithSync(); });

        var updates = manager.GetPackagesNeedingUpdate();

        if (updates.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]All packages are up to date![/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Current Version");
        table.AddColumn("New Version");
        table.AddColumn("Download Size");

        foreach (var pkg in updates.OrderBy(p => p.Name))
        {
            table.AddRow(
                pkg.Name,
                pkg.CurrentVersion,
                pkg.NewVersion,
                FormatSize(pkg.DownloadSize)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[yellow]{updates.Count} packages can be updated[/]");
        return 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}

public class PackageSettings : CommandSettings
{
    [CommandArgument(0, "<packages>")]
    [Description("Package name(s) to operate on")]
    public string[] Packages { get; set; } = [];

    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompt")]
    public bool NoConfirm { get; set; }
}

public class InstallCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]Packages to install:[/] {string.Join(", ", packageList)}");

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        using var manager = new AlpmManager();

        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            if (settings.NoConfirm)
            {
                // Machine-readable format for UI integration
                Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            }
            else
            {
                var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                args.Response = response ? 1 : 0;
            }
        };

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing and syncing ALPM...", ctx => { manager.IntializeWithSync(); });

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Installing packages...", ctx => { manager.InstallPackages(packageList); });

        AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
        return 0;
    }
}

public class RemoveCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]Packages to remove:[/] {string.Join(", ", packageList)}");

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        using var manager = new AlpmManager();

        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            if (settings.NoConfirm)
            {
                // Machine-readable format for UI integration
                Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            }
            else
            {
                var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                args.Response = response ? 1 : 0;
            }
        };

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing ALPM...", ctx => { manager.Initialize(true); });

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Removing packages...", ctx => { manager.RemovePackages(packageList); });

        AnsiConsole.MarkupLine("[green]Packages removed successfully![/]");
        return 0;
    }
}

public class UpdateCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]Packages to update:[/] {string.Join(", ", packageList)}");

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        using var manager = new AlpmManager();

        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            if (settings.NoConfirm)
            {
                // Machine-readable format for UI integration
                Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            }
            else
            {
                var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                args.Response = response ? 1 : 0;
            }
        };

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing and syncing ALPM...", ctx => { manager.IntializeWithSync(); });

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Updating packages...", ctx => { manager.UpdatePackages(packageList); });

        AnsiConsole.MarkupLine("[green]Packages updated successfully![/]");
        return 0;
    }
}

public class UpgradeSettings : CommandSettings
{
    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompt")]
    public bool NoConfirm { get; set; }
}

public class UpgradeCommand : Command<UpgradeSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] UpgradeSettings settings)
    {
        AnsiConsole.MarkupLine("[yellow]Performing full system upgrade...[/]");

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed with system upgrade?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        using var manager = new AlpmManager();

        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            if (settings.NoConfirm)
            {
                // Machine-readable format for UI integration
                Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            }
            else
            {
                var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                args.Response = response ? 1 : 0;
            }
        };

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing and syncing ALPM...", ctx => { manager.IntializeWithSync(); });

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Upgrading system...", ctx => { manager.SyncSystemUpdate(); });

        AnsiConsole.MarkupLine("[green]System upgraded successfully![/]");
        return 0;
    }
}

public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}

// Keyring command settings
public class KeyringSettings : CommandSettings
{
    [CommandArgument(0, "[keys]")]
    [Description("Key IDs or fingerprints to operate on")]
    public string[]? Keys { get; set; }

    [CommandOption("--keyserver <server>")]
    [Description("Keyserver to use for receiving keys")]
    public string? Keyserver { get; set; }
}

// Helper class for running pacman-key commands
public static class PacmanKeyRunner
{
    public static int Run(string args)
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pacman-key",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) AnsiConsole.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) AnsiConsole.MarkupLine($"[red]{Markup.Escape(e.Data)}[/]");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }
}

public class KeyringInitCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Initializing pacman keyring...[/]");
        var result = PacmanKeyRunner.Run("--init");
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keyring initialized successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to initialize keyring.[/]");
        }

        return result;
    }
}

public class KeyringPopulateCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        var args = "--populate";
        if (settings.Keys?.Length > 0)
        {
            args += " " + string.Join(" ", settings.Keys);
            AnsiConsole.MarkupLine($"[yellow]Populating keyring with: {string.Join(", ", settings.Keys)}...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Populating keyring with default keys...[/]");
        }

        var result = PacmanKeyRunner.Run(args);
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keyring populated successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to populate keyring.[/]");
        }

        return result;
    }
}

public class KeyringRecvCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (settings.Keys == null || settings.Keys.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No key IDs specified[/]");
            return 1;
        }

        var args = "--recv-keys " + string.Join(" ", settings.Keys);
        if (!string.IsNullOrEmpty(settings.Keyserver))
        {
            args += $" --keyserver {settings.Keyserver}";
        }

        AnsiConsole.MarkupLine($"[yellow]Receiving keys: {string.Join(", ", settings.Keys)}...[/]");
        var result = PacmanKeyRunner.Run(args);
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keys received successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to receive keys.[/]");
        }

        return result;
    }
}

public class KeyringLsignCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (settings.Keys == null || settings.Keys.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No key IDs specified[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[yellow]Locally signing keys: {string.Join(", ", settings.Keys)}...[/]");

        foreach (var key in settings.Keys)
        {
            var result = PacmanKeyRunner.Run($"--lsign-key {key}");
            if (result != 0)
            {
                AnsiConsole.MarkupLine($"[red]Failed to sign key: {key}[/]");
                return result;
            }
        }

        AnsiConsole.MarkupLine("[green]Keys signed successfully![/]");
        return 0;
    }
}

public class KeyringListCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Listing keys in keyring...[/]");
        return PacmanKeyRunner.Run("--list-keys");
    }
}

public class KeyringRefreshCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Refreshing keys from keyserver...[/]");
        var result = PacmanKeyRunner.Run("--refresh-keys");
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keys refreshed successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to refresh keys.[/]");
        }

        return result;
    }
}
