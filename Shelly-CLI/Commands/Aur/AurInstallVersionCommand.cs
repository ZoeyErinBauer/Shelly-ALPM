using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurInstallVersionCommand : AsyncCommand<AurInstallVersionSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] AurInstallVersionSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeInstallVersion(settings);
        }

        AurPackageManager? manager = null;
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
            manager = new AurPackageManager();
            await manager.Initialize(root: true);

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
            await manager.InstallPackageVersion(settings.Package, settings.Commit);
            AnsiConsole.MarkupLine("[green]Installation complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Installation failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }

    private static async Task<int> HandleUiModeInstallVersion(AurInstallVersionSettings settings)
    {
        AurPackageManager? manager = null;
        if (string.IsNullOrWhiteSpace(settings.Package))
        {
            Console.Error.WriteLine("Error: No package specified.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Commit))
        {
            Console.Error.WriteLine("Error: No commit specified.");
            return 1;
        }

        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true);

            manager.PackageProgress += (sender, args) =>
            {
                Console.Error.WriteLine($"[{args.CurrentIndex}/{args.TotalCount}] {args.PackageName}: {args.Status}" +
                    (args.Message != null ? $" - {args.Message}" : ""));
            };

            Console.Error.WriteLine($"Installing AUR package {settings.Package} at commit {settings.Commit}");
            await manager.InstallPackageVersion(settings.Package, settings.Commit);
            Console.Error.WriteLine("Installation complete.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Installation failed: {ex.Message}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}
