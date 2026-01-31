using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class SyncCommand : Command<SyncSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] SyncSettings settings)
    {
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
