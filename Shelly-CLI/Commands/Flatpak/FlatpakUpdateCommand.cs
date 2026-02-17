using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlatpakUpdateCommand : Command<FlatpakPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakPackageSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeUpdate(settings);
        }

        AnsiConsole.MarkupLine("[yellow]Updating flatpak app...[/]");
        var manager = new FlatpakManager();
        var result = manager.UpdateApp(settings.Packages);

        AnsiConsole.MarkupLine("[yellow]" + result.EscapeMarkup() + "[/]");

        return 0;
    }

    private static int HandleUiModeUpdate(FlatpakPackageSettings settings)
    {
        Console.Error.WriteLine("Updating flatpak app...");
        var manager = new FlatpakManager();
        var result = manager.UpdateApp(settings.Packages);

        Console.Error.WriteLine(result);

        return 0;
    }
}
