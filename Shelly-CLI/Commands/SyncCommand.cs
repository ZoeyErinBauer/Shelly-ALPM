using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class SyncSettings : CommandSettings
{
    [CommandOption("-f|--force")]
    [Description("Force synchronization even if databases are up to date")]
    public bool Force { get; set; }
}

public class SyncCommand : Command<SyncSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] SyncSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
        }

        using var manager = new AlpmManager();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing ALPM...", ctx => { manager.Initialize(true); });

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Synchronizing package databases...", ctx => { manager.Sync(settings.Force); });

        AnsiConsole.MarkupLine("[green]Package databases synchronized successfully![/]");
        return 0;
    }
}
