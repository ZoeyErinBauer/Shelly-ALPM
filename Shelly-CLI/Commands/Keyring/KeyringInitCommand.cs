using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringInitCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Initializing pacman keyring...[/]");
        var result = PacmanKeyRunner.Run("--init");
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keyring initialized successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to initialize keyring.[/]");
        }

        return result;
    }
}
