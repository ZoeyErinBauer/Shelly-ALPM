using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurListUpdatesCommand : AsyncCommand<DefaultSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] DefaultSettings settings)
    {
        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize();

            var updates = manager.GetPackagesNeedingUpdate().GetAwaiter().GetResult();

            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(updates, ShellyCLIJsonContext.Default.ListAurUpdateDto);
                await using var stdout = System.Console.OpenStandardOutput();
                await using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
                await writer.WriteLineAsync(json);
                await writer.FlushAsync();
                return 0;
            }

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
                    (pkg.Description ?? "No Description Available").EscapeMarkup().Truncate(50)
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
        finally
        {
            manager?.Dispose();
        }
    }
}