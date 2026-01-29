using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlathubGetRemote : Command<DefaultSettings>
{
    public override int Execute([NotNull] CommandContext context,[NotNull] DefaultSettings settings)
    {
        var result = new FlatpakManager().GetAvailableAppsFromAppstreamJson("flathub");
        
        AnsiConsole.MarkupLine(result.EscapeMarkup());
        return 0;
    }
}