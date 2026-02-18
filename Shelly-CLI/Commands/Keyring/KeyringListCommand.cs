using System.Diagnostics.CodeAnalysis;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringListCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeList();
        }

        AnsiConsole.MarkupLine("[yellow]Listing keys in keyring...[/]");
        return PacmanKeyRunner.Run("--list-keys");
    }

    private static int HandleUiModeList()
    {
        Console.Error.WriteLine("Listing keys in keyring...");
        return PacmanKeyRunner.Run("--list-keys");
    }
}
