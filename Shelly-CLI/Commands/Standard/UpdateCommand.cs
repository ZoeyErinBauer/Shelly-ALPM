using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

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
        bool isUpdateAll = packageList.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase));

        if (isUpdateAll)
        {
            AnsiConsole.MarkupLine("[yellow]Updating all packages (System Upgrade)...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Packages to update:[/] {string.Join(", ", packageList)}");
        }

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        using var manager = new AlpmManager();

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
            .Start("[yellow]Initializing and syncing ALPM...[/]", ctx =>
            {
                manager.IntializeWithSync();
            });

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
                var mainTask = ctx.AddTask("[green]Updating packages[/]", maxValue: isUpdateAll ? 100 : packageList.Count);
                var detailTask = ctx.AddTask("[blue]Waiting...[/]", maxValue: 100);

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

                if (isUpdateAll)
                {
                    manager.UpdateAll();
                }
                else
                {
                    manager.UpdatePackages(packageList);
                }
                
                mainTask.Value = mainTask.MaxValue;
                detailTask.Value = 100;
                detailTask.Description = "[green]Done[/]";
            });

        AnsiConsole.MarkupLine("[green]Packages updated successfully![/]");
        return 0;
    }
}
