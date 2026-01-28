using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurInstallVersionCommand : Command<AurInstallVersionSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AurInstallVersionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Package))
        {
            AnsiConsole.MarkupLine("[red]No package specified.[/]");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Commit))
        {
            AnsiConsole.MarkupLine("[red]No commit specified.[/]");
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

            AnsiConsole.MarkupLine(
                $"[yellow]Installing AUR package {settings.Package} at commit {settings.Commit}[/]");
            manager.InstallPackageVersion(settings.Package, settings.Commit).GetAwaiter().GetResult();
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
