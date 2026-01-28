using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpgradeCommand : Command<AurUpgradeSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AurUpgradeSettings settings)
    {
        try
        {
            var manager = new AurPackageManager();
            manager.Initialize(root: true).GetAwaiter().GetResult();

            var updates = manager.GetPackagesNeedingUpdate().GetAwaiter().GetResult();

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

            manager.PackageProgress += (sender, args) =>
            {
                var statusColor = args.Status switch
                {
                    PackageProgressStatus.Downloading => "yellow",
                    PackageProgressStatus.Building => "blue",
                    PackageProgressStatus.Installing => "cyan",
                    PackageProgressStatus.Completed => "green",
                    PackageProgressStatus.Failed => "red",
                    _ => "white"
                };

                AnsiConsole.MarkupLine(
                    $"[{statusColor}][[{args.CurrentIndex}/{args.TotalCount}]] {args.PackageName}: {args.Status}[/]" +
                    (args.Message != null ? $" - {args.Message.EscapeMarkup()}" : ""));
            };

            manager.PkgbuildDiffRequest += (sender, args) =>
            {
                if (settings.NoConfirm)
                {
                    args.ProceedWithUpdate = true;
                    return;
                }

                var showDiff = AnsiConsole.Confirm(
                    $"[yellow]PKGBUILD changed for {args.PackageName}. View diff?[/]", defaultValue: false);

                if (showDiff)
                {
                    AnsiConsole.MarkupLine("[blue]--- Old PKGBUILD ---[/]");
                    AnsiConsole.WriteLine(args.OldPkgbuild);
                    AnsiConsole.MarkupLine("[blue]--- New PKGBUILD ---[/]");
                    AnsiConsole.WriteLine(args.NewPkgbuild);
                }

                args.ProceedWithUpdate = AnsiConsole.Confirm(
                    $"[yellow]Proceed with update for {args.PackageName}?[/]", defaultValue: true);
            };

            var packageNames = updates.Select(u => u.Name).ToList();
            manager.UpdatePackages(packageNames).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine("[green]Upgrade complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Upgrade failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
