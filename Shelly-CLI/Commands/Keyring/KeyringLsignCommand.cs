using System.Diagnostics.CodeAnalysis;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringLsignCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeLsign(settings);
        }

        if (settings.Keys == null || settings.Keys.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No key IDs specified[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[yellow]Locally signing keys: {string.Join(", ", settings.Keys)}...[/]");

        foreach (var key in settings.Keys)
        {
            var result = PacmanKeyRunner.Run($"--lsign-key {key}");
            if (result != 0)
            {
                AnsiConsole.MarkupLine($"[red]Failed to sign key: {key}[/]");
                return result;
            }
        }

        AnsiConsole.MarkupLine("[green]Keys signed successfully![/]");
        return 0;
    }

    private static int HandleUiModeLsign(KeyringSettings settings)
    {
        if (settings.Keys == null || settings.Keys.Length == 0)
        {
            Console.Error.WriteLine("Error: No key IDs specified");
            return 1;
        }

        Console.Error.WriteLine($"Locally signing keys: {string.Join(", ", settings.Keys)}...");

        foreach (var key in settings.Keys)
        {
            var result = PacmanKeyRunner.Run($"--lsign-key {key}");
            if (result != 0)
            {
                Console.Error.WriteLine($"Failed to sign key: {key}");
                return result;
            }
        }

        Console.Error.WriteLine("Keys signed successfully!");
        return 0;
    }
}
