using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlatpakKillCommand : Command<FlatpakPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakPackageSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeKill(settings);
        }

        AnsiConsole.MarkupLine("[yellow]Killing selected flatpak app...[/]");
        var result = new FlatpakManager().KillApp(settings.Packages);

        AnsiConsole.MarkupLine("[red]" + result + "[/]");

        return 0;
    }

    private static int HandleUiModeKill(FlatpakPackageSettings settings)
    {
        Console.Error.WriteLine("Killing selected flatpak app...");
        var result = new FlatpakManager().KillApp(settings.Packages);

        Console.Error.WriteLine(result);

        return 0;
    }
}
