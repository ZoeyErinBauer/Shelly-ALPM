using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class ListAvailableCommand : Command<DefaultSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] DefaultSettings settings)
    {
        try
        {
            using var manager = new AlpmManager();

            if (!settings.JsonOutput)
            {
                if (settings.Sync)
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .Start("Initializing and syncing ALPM...", ctx => { manager.IntializeWithSync(); });
                }
                else
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .Start("Initializing ALPM...", ctx => { manager.Initialize(); });
                }
            }
            else
            {
                if (settings.Sync)
                {
                    manager.IntializeWithSync();
                }
                else
                {
                    manager.Initialize();
                }
            }

            var packages = manager.GetAvailablePackages();

            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(packages, ShellyCLIJsonContext.Default.ListAlpmPackageDto);
                // Write directly to stdout stream to bypass Spectre.Console redirection
                using var stdout = Console.OpenStandardOutput();
                using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
                writer.WriteLine(json);
                writer.Flush();
                return 0;
            }

            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Version");
            table.AddColumn("Repository");
            table.AddColumn("Description");

            foreach (var pkg in packages.OrderBy(p => p.Name).Take(100))
            {
                table.AddRow(
                    pkg.Name,
                    pkg.Version,
                    pkg.Repository,
                    pkg.Description.EscapeMarkup().Truncate(50)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[blue]Showing first 100 of {packages.Count} available packages[/]");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Exception: {ex.Message}");
            Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}
