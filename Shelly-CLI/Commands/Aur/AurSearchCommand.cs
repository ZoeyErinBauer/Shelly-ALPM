using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurSearchCommand : Command<AurSearchSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AurSearchSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            AnsiConsole.MarkupLine("[red]Query cannot be empty.[/]");
            return 1;
        }

        try
        {
            var manager = new AurPackageManager();
            manager.Initialize().GetAwaiter().GetResult();

            var results = manager.SearchPackages(settings.Query).GetAwaiter().GetResult();
            
            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(results, ShellyCLIJsonContext.Default.ListAurPackageDto);
                using var stdout = System.Console.OpenStandardOutput();
                using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
                writer.WriteLine(json);
                writer.Flush();
                return 0;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Version");
            table.AddColumn("Description");

            foreach (var pkg in results.Take(25))
            {
                table.AddRow(
                    pkg.Name.EscapeMarkup(),
                    pkg.Version.EscapeMarkup(),
                    (pkg.Description ?? "").EscapeMarkup().Truncate(60)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[blue]Total results:[/] {results.Count}");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Search failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
