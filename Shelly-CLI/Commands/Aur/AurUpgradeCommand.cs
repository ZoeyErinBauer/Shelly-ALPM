using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpgradeCommand : AsyncCommand<AurUpgradeSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context,
        [NotNull] AurUpgradeSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeUpgrade(settings);
        }
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
            await manager.UpdatePackages(packageNames);
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

    private static async Task<int> HandleUiModeUpgrade(AurUpgradeSettings settings)
    {
        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true);

            var updates = await manager.GetPackagesNeedingUpdate();

            if (updates.Count == 0)
            {
                Console.Error.WriteLine("All AUR packages are up to date.");
                return 0;
            }

            Console.Error.WriteLine($"{updates.Count} AUR packages need updates:");
            foreach (var pkg in updates)
            {
                Console.Error.WriteLine($"  {pkg.Name}: {pkg.Version} -> {pkg.NewVersion}");
            }

            manager.PackageProgress += (sender, args) =>
            {
                Console.Error.WriteLine($"[{args.CurrentIndex}/{args.TotalCount}] {args.PackageName}: {args.Status}" +
                    (args.Message != null ? $" - {args.Message}" : ""));
            };

            manager.PkgbuildDiffRequest += (sender, args) =>
            {
                if (settings.NoConfirm)
                {
                    args.ProceedWithUpdate = true;
                    return;
                }

                Console.Error.WriteLine($"PKGBUILD changed for {args.PackageName}.");
                Console.Error.WriteLine("--- Old PKGBUILD ---");
                Console.Error.WriteLine(args.OldPkgbuild);
                Console.Error.WriteLine("--- New PKGBUILD ---");
                Console.Error.WriteLine(args.NewPkgbuild);
                args.ProceedWithUpdate = true;
            };

            var packageNames = updates.Select(u => u.Name).ToList();
            await manager.UpdatePackages(packageNames);
            Console.Error.WriteLine("Upgrade complete.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Upgrade failed: {ex.Message}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}