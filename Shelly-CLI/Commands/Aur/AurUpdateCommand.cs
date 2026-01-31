using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpdateCommand : AsyncCommand<AurPackageSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context,
        [NotNull] AurPackageSettings settings)
    {
        var packageList = settings.Packages.ToList();
        bool isUpdateAll = packageList.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase));

        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true);

            if (isUpdateAll)
            {
                var updates = await manager.GetPackagesNeedingUpdate();
                if (updates.Count == 0)
                {
                    AnsiConsole.MarkupLine("[green]All AUR packages are up to date.[/]");
                    return 0;
                }
                packageList = updates.Select(u => u.Name).ToList();
                AnsiConsole.MarkupLine($"[yellow]Updating {packageList.Count} AUR packages...[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Updating AUR packages:[/] {string.Join(", ", packageList)}");
            }

            if (!settings.NoConfirm)
            {
                if (!AnsiConsole.Confirm("Do you want to proceed?"))
                {
                    AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                    return 0;
                }
            }

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    var mainTask = ctx.AddTask("[green]AUR Update[/]", maxValue: packageList.Count);
                    var detailTask = ctx.AddTask("[blue]Initializing[/]", maxValue: 100);

                    manager.PackageProgress += (sender, args) =>
                    {
                        mainTask.Value = args.CurrentIndex;
                        detailTask.Description = $"[cyan]{args.PackageName}[/]: {args.Status}";
                        detailTask.Value = args.Status == PackageProgressStatus.Completed ? 100 : 0;

                        if (args.Status == PackageProgressStatus.Completed)
                        {
                            mainTask.Increment(1);
                        }
                    };

                    manager.PkgbuildDiffRequest += (sender, args) =>
                    {
                        if (settings.NoConfirm)
                        {
                            args.ProceedWithUpdate = true;
                            return;
                        }

                        AnsiConsole.MarkupLine($"\n[yellow]PKGBUILD changed for {args.PackageName}.[/]");
                        if (AnsiConsole.Confirm("View diff?", defaultValue: false))
                        {
                            AnsiConsole.WriteLine(args.OldPkgbuild);
                            AnsiConsole.WriteLine("---");
                            AnsiConsole.WriteLine(args.NewPkgbuild);
                        }

                        args.ProceedWithUpdate = AnsiConsole.Confirm($"Proceed with update for {args.PackageName}?", defaultValue: true);
                    };

                    await manager.UpdatePackages(packageList);
                    
                    mainTask.Value = packageList.Count;
                    detailTask.Value = 100;
                    detailTask.Description = "[green]Complete[/]";
                });

            AnsiConsole.MarkupLine("[green]Update complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Update failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}
