using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Aur;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurSearchCommand : AsyncCommand<AurSearchSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] AurSearchSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeSearch(settings);
        }

        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            AnsiConsole.MarkupLine("[red]Query cannot be empty.[/]");
            return 1;
        }

        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize();

            var results = manager.SearchPackages(settings.Query).GetAwaiter().GetResult();

            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(results, ShellyCLIJsonContext.Default.ListAurPackageDto);
                await using var stdout = System.Console.OpenStandardOutput();
                await using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
                await writer.WriteLineAsync(json);
                await writer.FlushAsync();
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
        finally
        {
            manager?.Dispose();
        }
    }

    private static async Task<int> HandleUiModeSearch(AurSearchSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            Console.Error.WriteLine("Error: Query cannot be empty.");
            return 1;
        }

        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize();

            var results = manager.SearchPackages(settings.Query).GetAwaiter().GetResult();

            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(results, ShellyCLIJsonContext.Default.ListAurPackageDto);
                await using var stdout = Console.OpenStandardOutput();
                await using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
                await writer.WriteLineAsync(json);
                await writer.FlushAsync();
                return 0;
            }

            foreach (var pkg in results.Take(25))
            {
                Console.WriteLine($"{pkg.Name} {pkg.Version} - {pkg.Description ?? ""}");
            }

            Console.Error.WriteLine($"Total results: {results.Count}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Search failed: {ex.Message}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}