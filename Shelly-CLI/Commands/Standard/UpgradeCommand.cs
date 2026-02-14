using System;
using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class UpgradeCommand : Command<UpgradeSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] UpgradeSettings settings)
    {
        Dictionary<string, int> packageProgress = new();
        AnsiConsole.MarkupLine("[yellow]Performing full system upgrade...[/]");

     

        using var manager = new AlpmManager();

        manager.Progress += (sender, args) =>
        {
            if (packageProgress.TryGetValue(args.PackageName!, out int value) && value >= args.Percent) return;
            packageProgress[args.PackageName!] = args.Percent ?? 0;
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {packageProgress[args.PackageName!]}%");
        };

        manager.Replaces += (sender, args) =>
        {
            foreach (var replace in args.Replaces)
            {
                AnsiConsole.MarkupLine($"[magenta]Replacement:[/] [cyan]{args.Repository}/{args.PackageName}[/] replaces [red]{replace}[/]");
            }
        };

        manager.Question += (sender, args) =>
        {
            // Handle SelectProvider differently - it needs a selection, not yes/no
            if (args.QuestionType == AlpmQuestionType.SelectProvider && args.ProviderOptions?.Count > 0)
            {
                if (settings.NoConfirm)
                {
                    if (Program.IsUiMode)
                    {
                        // Machine-readable format for UI integration
                        Console.Error.WriteLine($"[Shelly][ALPM_SELECT_PROVIDER]{args.DependencyName}");
                        for (int i = 0; i < args.ProviderOptions.Count; i++)
                        {
                            Console.Error.WriteLine($"[Shelly][ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                        }
                        Console.Error.WriteLine("[Shelly][ALPM_PROVIDER_END]");
                        Console.Error.Flush();
                        var input = Console.ReadLine();
                        args.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
                    }
                    else
                    {
                        // Non-interactive CLI mode: default to the first provider
                        args.Response = 0;
                    }
                }
                else
                {
                    var selection = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title($"[yellow]{args.QuestionText}[/]")
                            .AddChoices(args.ProviderOptions));
                    args.Response = args.ProviderOptions.IndexOf(selection);
                }
            }
            else if (settings.NoConfirm)
            {
                if (Program.IsUiMode)
                {
                    // Machine-readable format for UI integration
                    Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                    Console.Error.Flush();
                    var input = Console.ReadLine();
                    args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
                }
                else
                {
                    // Non-interactive CLI mode: automatically confirm
                    args.Response = 1;
                }
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
        var packagesNeedingUpdate = manager.GetPackagesNeedingUpdate();
        if (packagesNeedingUpdate.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]System is up to date![/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Package");
        table.AddColumn("Current Version");
        table.AddColumn("New Version");
        table.AddColumn("Download Size");
        foreach (var pkg in packagesNeedingUpdate)
        {
            table.AddRow(pkg.Name, pkg.CurrentVersion, pkg.NewVersion, pkg.DownloadSize.ToString());
        }
        AnsiConsole.Write(table);
        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed with system upgrade?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }
        AnsiConsole.MarkupLine("[yellow] Starting System Upgrade...[/]");
        manager.SyncSystemUpdate();

        AnsiConsole.MarkupLine("[green]System upgraded successfully![/]");
        return 0;
    }
}
