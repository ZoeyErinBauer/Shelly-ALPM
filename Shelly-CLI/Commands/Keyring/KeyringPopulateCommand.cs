using System.Diagnostics.CodeAnalysis;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringPopulateCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModePopulate(settings);
        }

        var args = "--populate";
        if (settings.Keys?.Length > 0)
        {
            args += " " + string.Join(" ", settings.Keys);
            AnsiConsole.MarkupLine($"[yellow]Populating keyring with: {string.Join(", ", settings.Keys)}...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Populating keyring with default keys...[/]");
        }

        var result = PacmanKeyRunner.Run(args);
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keyring populated successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to populate keyring.[/]");
        }

        return result;
    }

    private static int HandleUiModePopulate(KeyringSettings settings)
    {
        var args = "--populate";
        if (settings.Keys?.Length > 0)
        {
            args += " " + string.Join(" ", settings.Keys);
            Console.Error.WriteLine($"Populating keyring with: {string.Join(", ", settings.Keys)}...");
        }
        else
        {
            Console.Error.WriteLine("Populating keyring with default keys...");
        }

        var result = PacmanKeyRunner.Run(args);
        if (result == 0)
        {
            Console.Error.WriteLine("Keyring populated successfully!");
        }
        else
        {
            Console.Error.WriteLine("Failed to populate keyring.");
        }

        return result;
    }
}
