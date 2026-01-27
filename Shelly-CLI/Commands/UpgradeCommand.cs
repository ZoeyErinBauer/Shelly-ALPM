using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

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
        Dictionary<string, int> packageProgress = new();
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
            if (packageProgress.TryGetValue(args.PackageName, out int value) && value >= args.Percent) return;
            packageProgress[args.PackageName] = args.Percent ?? 0;
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {packageProgress[args.PackageName]}%");
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

        AnsiConsole.MarkupLine("[yellow]Checking for system updates...[/]");
        AnsiConsole.MarkupLine("[yellow] Initializing and syncing repositories...[/]");
        manager.IntializeWithSync();
        AnsiConsole.MarkupLine("[yellow] Starting System Upgrade...[/]");
        manager.SyncSystemUpdate();

        AnsiConsole.MarkupLine("[green]System upgraded successfully![/]");
        return 0;
    }
}