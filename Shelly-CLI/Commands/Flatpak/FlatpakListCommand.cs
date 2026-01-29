using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlatpakListCommand : Command<DefaultSettings>
{
    public override int Execute([NotNull] CommandContext context,[NotNull] DefaultSettings settings)
    {
        var manager = new FlatpakManager();

        var packages = manager.SearchInstalled();
        
        if (settings.JsonOutput)
        {
            var json = JsonSerializer.Serialize(packages, FlatpakDtoJsonContext.Default.ListFlatpakPackageDto);
            AnsiConsole.MarkupLine(json.EscapeMarkup());
            return 0;
        }
        
        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Id");
        table.AddColumn("Version");
        table.AddColumn("Arch");
        table.AddColumn("Branch");
        table.AddColumn("Summary");

        foreach (var pkg in packages.OrderBy(p => p.Id))
        {
            table.AddRow(
                pkg.Name,
                pkg.Id,
                pkg.Version,
                pkg.Arch,
                pkg.Version,
                pkg.Summary.EscapeMarkup().Truncate(50)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Total: packages[/]");
        return 0;
    }
}
