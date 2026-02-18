using System.Diagnostics.CodeAnalysis;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringRefreshCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeRefresh();
        }

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

    private static int HandleUiModeRefresh()
    {
        Console.Error.WriteLine("Refreshing keys from keyserver...");
        var result = PacmanKeyRunner.Run("--refresh-keys");
        if (result == 0)
        {
            Console.Error.WriteLine("Keys refreshed successfully!");
        }
        else
        {
            Console.Error.WriteLine("Failed to refresh keys.");
        }

        return result;
    }
}
