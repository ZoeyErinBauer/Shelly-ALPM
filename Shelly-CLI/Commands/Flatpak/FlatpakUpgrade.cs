using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlatpakUpgrade : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Updating all flatpak apps...[/]");
        var manager = new FlatpakManager();
        var result = manager.UpdateAllFlatpak();

        AnsiConsole.MarkupLine("[yellow]" + result.EscapeMarkup() + "[/]");

        return 0;
    }
}