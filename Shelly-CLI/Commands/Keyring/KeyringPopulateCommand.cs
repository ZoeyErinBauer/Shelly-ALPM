using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringPopulateCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
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
}
