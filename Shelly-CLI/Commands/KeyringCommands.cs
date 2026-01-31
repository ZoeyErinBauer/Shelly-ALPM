using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class KeyringSettings : CommandSettings
{
    [CommandArgument(0, "[keys]")]
    [Description("Key IDs or fingerprints to operate on")]
    public string[]? Keys { get; set; }

    [CommandOption("--keyserver <server>")]
    [Description("Keyserver to use for receiving keys")]
    public string? Keyserver { get; set; }
}

public class KeyringInitCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
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
}

public class KeyringPopulateCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
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
}

public class KeyringRecvCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
        }

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

public class KeyringLsignCommand : Command<KeyringSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] KeyringSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
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
}

public class KeyringListCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]Listing keys in keyring...[/]");
        return PacmanKeyRunner.Run("--list-keys");
    }
}

public class KeyringRefreshCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
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
}
