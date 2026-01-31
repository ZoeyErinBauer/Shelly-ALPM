using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringListCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Listing keys in keyring...[/]");
        return PacmanKeyRunner.Run("--list-keys");
    }
}
