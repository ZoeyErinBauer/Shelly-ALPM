using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurInstallCommand : Command<AurPackageSettings>
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

            AnsiConsole.MarkupLine($"[yellow]Installing AUR packages: {string.Join(", ", settings.Packages)}[/]");
            manager.InstallPackages(settings.Packages.ToList()).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine("[green]Installation complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Installation failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
