using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurListInstalledCommand : AsyncCommand<ListSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] ListSettings settings)
    {
        AurPackageManager manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize();

            var packages = await manager.GetInstalledPackages();

            // Apply filter if specified
            if (!string.IsNullOrWhiteSpace(settings.Filter))
            {
                packages = packages.Where(p => p.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Apply sorting based on settings
            // Note: Size sorts by name as there is no size data available for AUR packages
            var sortedPackages = settings.Sort switch
            {
                SortOption.Popularity => settings.Order == SortDirection.Ascending
                    ? packages.OrderBy(p => p.Popularity)
                    : packages.OrderByDescending(p => p.Popularity),
                SortOption.Size => settings.Order == SortDirection.Ascending
                    ? packages.OrderBy(p => p.Name)
                    : packages.OrderByDescending(p => p.Name),
                _ => settings.Order == SortDirection.Ascending
                    ? packages.OrderBy(p => p.Name)
                    : packages.OrderByDescending(p => p.Name)
            };

            if (settings.JsonOutput)
            {
                var sortedList = sortedPackages.ToList();
                var json = JsonSerializer.Serialize(sortedList, ShellyCLIJsonContext.Default.ListAurPackageDto);
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
            
            var skip = (settings.Page - 1) * settings.Take;
            var displayPackages = sortedPackages.Skip(skip).Take(settings.Take).ToList();

            foreach (var pkg in displayPackages)
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
