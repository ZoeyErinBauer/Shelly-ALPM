using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpdateCommand : Command<AurPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AurPackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No packages specified.[/]");
            return 1;
        }

        try
        {
            var manager = new AurPackageManager();
            manager.Initialize(root: true).GetAwaiter().GetResult();

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

            AnsiConsole.MarkupLine($"[yellow]Updating AUR packages: {string.Join(", ", settings.Packages)}[/]");
            manager.UpdatePackages(settings.Packages.ToList()).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine("[green]Update complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Update failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
