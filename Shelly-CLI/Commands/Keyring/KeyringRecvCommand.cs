using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringRecvCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (settings.Keys == null || settings.Keys.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No key IDs specified[/]");
            return 1;
        }

        var args = "--recv-keys " + string.Join(" ", settings.Keys);
        if (!string.IsNullOrEmpty(settings.Keyserver))
        {
            args += $" --keyserver {settings.Keyserver}";
        }

        AnsiConsole.MarkupLine($"[yellow]Receiving keys: {string.Join(", ", settings.Keys)}...[/]");
        var result = PacmanKeyRunner.Run(args);
        if (result == 0)
        {
            AnsiConsole.MarkupLine("[green]Keys received successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to receive keys.[/]");
        }

        return result;
    }
}
