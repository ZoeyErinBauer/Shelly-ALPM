using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlatpakRunCommand : Command<FlatpakPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakPackageSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeRun(settings);
        }

        AnsiConsole.MarkupLine("[yellow]Running selected flatpak app...[/]");
        var result = new FlatpakManager().LaunchApp(settings.Packages);

        if (result)
        {
            AnsiConsole.MarkupLine("[green]App launched successfully[/]");
            return 0;
       }

        AnsiConsole.MarkupLine("[red]Failed to launch app[/]");
        return 1;
    }

    private static int HandleUiModeRun(FlatpakPackageSettings settings)
    {
        Console.Error.WriteLine("Running selected flatpak app...");
        var result = new FlatpakManager().LaunchApp(settings.Packages);

        if (result)
        {
            Console.Error.WriteLine("App launched successfully");
            return 0;
        }

        Console.Error.WriteLine("Failed to launch app");
        return 1;
    }
}
