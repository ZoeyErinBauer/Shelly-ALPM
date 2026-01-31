using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurListInstalledCommand : AsyncCommand<DefaultSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] DefaultSettings settings)
    {
        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize();

            var packages = await manager.GetInstalledPackages();

            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(packages, ShellyCLIJsonContext.Default.ListAurPackageDto);
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
        finally
        {
            manager?.Dispose();
        }
    }
}
