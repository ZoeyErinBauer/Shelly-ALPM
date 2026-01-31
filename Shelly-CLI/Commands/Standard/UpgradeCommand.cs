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

        manager.Replaces += (sender, args) =>
        {
            foreach (var replace in args.Replaces)
            {
                AnsiConsole.MarkupLine($"[magenta]Replacement:[/] [cyan]{args.Repository}/{args.PackageName}[/] replaces [red]{replace}[/]");
            }
        };

        manager.Question += (sender, args) =>
        {
            if (settings.NoConfirm)
            {
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
            .Start("[yellow]Checking for system updates...[/]\n[yellow] Initializing and syncing repositories...[/]", ctx =>
            {
                manager.IntializeWithSync();
            });

        var packagesNeedingUpdate = manager.GetPackagesNeedingUpdate();
        if (packagesNeedingUpdate.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]System is up to date![/]");
            return 0;
        }

        AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            )
            .Start(ctx =>
            {
                var mainTask = ctx.AddTask("[green]System Upgrade[/]", maxValue: 100);
                var detailTask = ctx.AddTask("[blue]Initializing[/]", maxValue: 100);

                manager.Progress += (sender, args) =>
                {
                    if (args.HowMany.HasValue && args.HowMany.Value > 0)
                    {
                        mainTask.MaxValue = (double)args.HowMany.Value;
                        mainTask.Value = (double)args.Current.GetValueOrDefault();
                    }

                    detailTask.Description = $"[cyan]{args.PackageName ?? "Processing"}[/]";
                    detailTask.Value = args.Percent.GetValueOrDefault();
                };

                manager.SyncSystemUpdate();
                
                mainTask.Value = mainTask.MaxValue;
                detailTask.Value = 100;
                detailTask.Description = "[green]Complete[/]";
            });

        AnsiConsole.MarkupLine("[green]System upgraded successfully![/]");
        return 0;
    }
}
