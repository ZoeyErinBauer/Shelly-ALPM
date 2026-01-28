using System.Diagnostics.CodeAnalysis;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurListInstalledCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        try
        {
            var manager = new AurPackageManager();
            manager.Initialize().GetAwaiter().GetResult();

            var packages = manager.GetInstalledPackages().GetAwaiter().GetResult();

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Version");
            table.AddColumn("Description");

            foreach (var pkg in packages.OrderBy(p => p.Name))
            {
                table.AddRow(
                    pkg.Name.EscapeMarkup(),
                    pkg.Version.EscapeMarkup(),
                    (pkg.Description ?? "").EscapeMarkup().Truncate(60)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[blue]Total:[/] {packages.Count} AUR packages installed");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to list packages:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
