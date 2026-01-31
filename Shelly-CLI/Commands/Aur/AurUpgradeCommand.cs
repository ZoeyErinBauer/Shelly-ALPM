using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpgradeCommand : AsyncCommand<AurUpgradeSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context,
        [NotNull] AurUpgradeSettings settings)
    {
        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true);

            var updates = await manager.GetPackagesNeedingUpdate();

            if (updates.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]All AUR packages are up to date.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]{updates.Count} AUR packages need updates:[/]");
            foreach (var pkg in updates)
            {
                AnsiConsole.MarkupLine($"  {pkg.Name}: {pkg.Version} -> {pkg.NewVersion}");
            }

            if (!settings.NoConfirm)
            {
                if (!AnsiConsole.Confirm("[yellow]Proceed with upgrade?[/]", defaultValue: true))
                {
                    AnsiConsole.MarkupLine("[yellow]Upgrade cancelled.[/]");
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
                    var mainTask = ctx.AddTask("[green]Upgrading AUR packages[/]", maxValue: updates.Count);
                    var packageTask = ctx.AddTask("[blue]Initializing[/]", maxValue: 100);

                    manager.PackageProgress += (sender, args) =>
                    {
                        mainTask.Value = args.CurrentIndex;
                        packageTask.Description = $"[cyan]{args.PackageName}[/]: {args.Status}";
                        packageTask.Value = args.Status == PackageProgressStatus.Completed ? 100 : 0;

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

                        // We need to stop the progress display to interact with the console
                        // Actually, Spectre allows interaction if we handle it carefully, 
                        // but it's safer to just handle it before or using a separate mechanism.
                        // However, PkgbuildDiffRequest is triggered DURING UpdatePackages.
                        
                        AnsiConsole.MarkupLine($"\n[yellow]PKGBUILD changed for {args.PackageName}.[/]");
                        if (AnsiConsole.Confirm("View diff?", defaultValue: false))
                        {
                            AnsiConsole.WriteLine(args.OldPkgbuild);
                            AnsiConsole.WriteLine("---");
                            AnsiConsole.WriteLine(args.NewPkgbuild);
                        }

                        args.ProceedWithUpdate = AnsiConsole.Confirm($"Proceed with update for {args.PackageName}?", defaultValue: true);
                    };

                    var packageNames = updates.Select(u => u.Name).ToList();
                    await manager.UpdatePackages(packageNames);
                    
                    mainTask.Value = updates.Count;
                    packageTask.Value = 100;
                    packageTask.Description = "[green]Complete[/]";
                });

            AnsiConsole.MarkupLine("[green]Upgrade complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Upgrade failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}
