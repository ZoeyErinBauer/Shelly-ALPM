using System.Diagnostics.CodeAnalysis;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringInitCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeInit();
        }

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

    private static int HandleUiModeInit()
    {
        Console.Error.WriteLine("Initializing pacman keyring...");
        var result = PacmanKeyRunner.Run("--init");
        if (result == 0)
        {
            Console.Error.WriteLine("Keyring initialized successfully!");
        }
        else
        {
            Console.Error.WriteLine("Failed to initialize keyring.");
        }

        return result;
    }
}
