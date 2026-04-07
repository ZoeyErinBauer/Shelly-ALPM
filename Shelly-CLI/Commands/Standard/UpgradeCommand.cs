using PackageManager.Alpm;
using Shelly_CLI.Commands.Aur;
using Shelly_CLI.Commands.Flatpak;
using Shelly_CLI.Configuration;
using Shelly_CLI.ConsoleLayouts;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;
using static System.Enum;

namespace Shelly_CLI.Commands.Standard;

public class UpgradeCommand : AsyncCommand<UpgradeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context,UpgradeSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeUpgrade(context, settings);
        }

        RootElevator.EnsureRootExectuion();
        var archNews = new ArchNews();
        archNews.ExecuteAsync(context, new ArchNewsSettings()).GetAwaiter().GetResult();

        AnsiConsole.MarkupLine("[yellow]Performing full system upgrade...[/]");

        var manager = new AlpmManager();

        AnsiConsole.MarkupLine("[yellow]Checking for system updates...[/]");
        AnsiConsole.MarkupLine("[yellow]Initializing and syncing repositories...[/]");
        manager.Initialize(true);
        manager.Sync();
        var packagesNeedingUpdate = manager.GetPackagesNeedingUpdate();
        if (packagesNeedingUpdate.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]System is up to date![/]");
            return 0;
        }

        var config = ConfigManager.ReadConfig();
        var parsed =
            (SizeDisplay)Parse(typeof(SizeDisplay),
                config.FileSizeDisplay);

        var table = new Table();
        table.AddColumn("Package");
        table.AddColumn("Current Version");
        table.AddColumn("New Version");
        table.AddColumn($"Net Change ({config.FileSizeDisplay})");
        table.AddColumn($"Download Size ({config.FileSizeDisplay})");
        
        long totalDownloadSize = 0;
        long totalNetChange = 0;
        
        foreach (var pkg in packagesNeedingUpdate)
        {
            long oldInstalledSizeBytes =  pkg.DownloadSize; 
            long newInstalledSizeBytes = pkg.DownloadSize; 
            long netChangeBytes = newInstalledSizeBytes - oldInstalledSizeBytes;

           
            totalDownloadSize += pkg.DownloadSize;
            totalNetChange += netChangeBytes;

            table.AddRow(
                pkg.Name, 
                pkg.CurrentVersion, 
                pkg.NewVersion, 
                FormatSize(parsed, netChangeBytes), 
                FormatSize(parsed, pkg.DownloadSize) 
            );
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Total Download Size:[/] {FormatSize(parsed, totalDownloadSize)} {config.FileSizeDisplay}");

        string FormatSize(SizeDisplay size, double bytes)
        {
            throw new NotImplementedException();
        }

        AnsiConsole.MarkupLine($"[bold]Net Upgrade Size:[/]  {FormatSize(parsed, totalNetChange)} {config.FileSizeDisplay}");
        AnsiConsole.WriteLine();
        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed with system upgrade?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        AnsiConsole.MarkupLine("[yellow] Starting System Upgrade...[/]");
        await SplitOutput.Output(manager, x => x.SyncSystemUpdate(), settings.NoConfirm);
        AnsiConsole.MarkupLine("[green]System upgraded successfully![/]");
        manager.Dispose();
        if (settings.Aur || settings.All)
        {
            var aurCommand = new AurUpgradeCommand();
            var aurSettings = new AurUpgradeSettings()
            {
                NoConfirm = settings.NoConfirm
            };
            aurCommand.ExecuteAsync(context, aurSettings).GetAwaiter().GetResult();
        }

        if (settings.Flatpak || settings.All)
        {
            var flatpakCommand = new FlatpakUpgrade();
            flatpakCommand.Execute(context);
        }

        return 0;
    }

    private static async Task<int> HandleUiModeUpgrade(CommandContext context, UpgradeSettings settings)
    {
        await Console.Error.WriteLineAsync("Performing full system upgrade...");

        var manager = new AlpmManager();
        object renderLock = new();

        manager.Replaces += (_, args) =>
        {
            foreach (var replace in args.Replaces)
            {
                Console.Error.WriteLine(
                    $"Replacement: {args.Repository}/{args.PackageName} replaces {replace}");
            }
        };

        manager.Question += (_, args) =>
        {
            lock (renderLock)
            {
                Console.Error.WriteLine();
                QuestionHandler.HandleQuestion(args, Program.IsUiMode, settings.NoConfirm);
            }
        };

        await Console.Error.WriteLineAsync("Checking for system updates...");
        await Console.Error.WriteLineAsync(" Initializing and syncing repositories...");
        manager.IntializeWithSync();
        var packagesNeedingUpdate = manager.GetPackagesNeedingUpdate();
        if (packagesNeedingUpdate.Count == 0)
        {
            await Console.Error.WriteLineAsync("System is up to date!");
            return 0;
        }

        await Console.Error.WriteLineAsync($"{packagesNeedingUpdate.Count} packages need updates:");
        foreach (var pkg in packagesNeedingUpdate)
        {
            await Console.Error.WriteLineAsync(
                $"  {pkg.Name}: {pkg.CurrentVersion} -> {pkg.NewVersion} ({pkg.DownloadSize} bytes)");
        }

        await Console.Error.WriteLineAsync(" Starting System Upgrade...");

        manager.Progress += (_, args) =>
        {
            lock (renderLock)
            {
                var name = args.PackageName ?? "unknown";
                var pct = args.Percent ?? 0;
                var actionType = args.ProgressType;
                Console.Error.WriteLine($"{name}: {pct}% - {actionType}");
            }
        };

        manager.HookRun += (_, args) =>
        {
            Console.Error.WriteLine($"[ALPM_HOOK]{args.Description}");
        };

        await manager.SyncSystemUpdate();
        manager.Dispose();
        if (settings.Aur || settings.All)
        {
            var aurCommand = new AurUpgradeCommand();
            var aurSettings = new AurUpgradeSettings()
            {
                NoConfirm = settings.NoConfirm,
            };
            aurCommand.ExecuteAsync(context, aurSettings).GetAwaiter().GetResult();
        }

        if (settings.Flatpak || settings.All)
        {
            var flatpakCommand = new FlatpakUpgrade();
            flatpakCommand.Execute(context);
        }

        await Console.Error.WriteLineAsync("System upgraded successfully!");
        manager.Dispose();
        return 0;
    }

    private static string CalculateDownside(SizeDisplay size, long downloadSize)
    {
        return size switch
        {
            SizeDisplay.Bytes => downloadSize.ToString(),
            SizeDisplay.Megabytes => (downloadSize / 1048576.0).ToString("F2"),
            SizeDisplay.Gigabytes => (downloadSize / 1073741824.0).ToString("F2"),
            _ => downloadSize.ToString()
        };
    }
}