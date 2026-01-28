using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurRemoveCommand : Command<AurPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AurPackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No packages specified.[/]");
            return 1;
        }

        try
        {
            var manager = new AurPackageManager();
            manager.Initialize(root: true).GetAwaiter().GetResult();

            AnsiConsole.MarkupLine($"[yellow]Removing AUR packages: {string.Join(", ", settings.Packages)}[/]");
            manager.RemovePackages(settings.Packages.ToList()).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine("[green]Removal complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Removal failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
