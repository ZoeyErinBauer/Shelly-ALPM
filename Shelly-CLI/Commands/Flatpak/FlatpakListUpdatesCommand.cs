using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Flatpak;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlatpakListUpdatesCommand : Command<DefaultSettings>
{
    public override int Execute([NotNull] CommandContext context,[NotNull] DefaultSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeListUpdates(settings);
        }

        var manager = new FlatpakManager();

        var packages = FlatpakManager.GetPackagesWithUpdates(true);

        if (settings.JsonOutput)
        {
            var json = JsonSerializer.Serialize(packages, FlatpakDtoJsonContext.Default.ListFlatpakPackageDto);
            using var stdout = Console.OpenStandardOutput();
            using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            writer.WriteLine(json);
            writer.Flush();
            return 0;
        }
        
        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Id");
        table.AddColumn("Version");
        table.AddColumn("Permissions");

        foreach (var pkg in packages.OrderBy(p => p.Id))
        {
            var permissions = pkg.Permissions.Count > 0 
                ? string.Join("\n", pkg.Permissions) 
                : "[grey]No changes[/]";
                
            table.AddRow(
                pkg.Name,
                pkg.Id,
                pkg.Version,
                permissions
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Total: packages[/]");
        return 0;
    }

    private static int HandleUiModeListUpdates(DefaultSettings settings)
    {
        var manager = new FlatpakManager();

        var packages = FlatpakManager.GetPackagesWithUpdates(true);

        if (settings.JsonOutput)
        {
            var json = JsonSerializer.Serialize(packages, FlatpakDtoJsonContext.Default.ListFlatpakPackageDto);
            using var stdout = Console.OpenStandardOutput();
            using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            writer.WriteLine(json);
            writer.Flush();
            return 0;
        }

        foreach (var pkg in packages.OrderBy(p => p.Id))
        {
            Console.WriteLine($"{pkg.Name} {pkg.Id} {pkg.Version}");
        }

        Console.Error.WriteLine("Total: packages");
        return 0;
    }
}
