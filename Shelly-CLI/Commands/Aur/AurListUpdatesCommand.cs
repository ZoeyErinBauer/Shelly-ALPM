using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurListUpdatesCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        try
        {
            var manager = new AurPackageManager();
            manager.Initialize().GetAwaiter().GetResult();

            var updates = manager.GetPackagesNeedingUpdate().GetAwaiter().GetResult();

            if (updates.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]All AUR packages are up to date.[/]");
                return 0;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Installed");
            table.AddColumn("Available");
            table.AddColumn("Description");

            foreach (var pkg in updates.OrderBy(p => p.Name))
            {
                table.AddRow(
                    pkg.Name.EscapeMarkup(),
                    pkg.Version.EscapeMarkup(),
                    pkg.NewVersion.EscapeMarkup(),
                    pkg.Description.EscapeMarkup().Truncate(50)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[blue]Total:[/] {updates.Count} packages need updates");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to check updates:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
