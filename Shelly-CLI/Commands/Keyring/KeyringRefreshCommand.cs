using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringRefreshCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Refreshing keys from keyserver...[/]");
        var result = PacmanKeyRunner.Run("--refresh-keys");
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keys refreshed successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to refresh keys.[/]");
        }

        return result;
    }
}
