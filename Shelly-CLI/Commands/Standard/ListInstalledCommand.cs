using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using PackageManager.Alpm;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class ListInstalledCommand : Command<ListSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] ListSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeListInstalled(settings);
        }
        using var manager = new AlpmManager();

        if (!settings.JsonOutput)
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start("Initializing ALPM...", ctx => { manager.Initialize(true); });
        }
        else
        {
            manager.Initialize(true);
        }

        var packages = manager.GetInstalledPackages();

        // Apply filter if specified
        if (!string.IsNullOrWhiteSpace(settings.Filter))
        {
            packages = packages.Where(p => p.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply sorting based on settings
        // Note: Popularity sorts by name as there is no popularity data available for standard packages
        var sortedPackages = settings.Sort switch
        {
            SortOption.Size => settings.Order == SortDirection.Ascending
                ? packages.OrderBy(p => p.Size)
                : packages.OrderByDescending(p => p.Size),
            SortOption.Popularity => settings.Order == SortDirection.Ascending
                ? packages.OrderBy(p => p.Name)
                : packages.OrderByDescending(p => p.Name),
            _ => settings.Order == SortDirection.Ascending
                ? packages.OrderBy(p => p.Name)
                : packages.OrderByDescending(p => p.Name)
        };

        if (settings.JsonOutput)
        {
            var sortedList = sortedPackages.ToList();
            var json = JsonSerializer.Serialize(sortedList, ShellyCLIJsonContext.Default.ListAlpmPackageDto);
            // Write directly to stdout stream to bypass Spectre.Console redirection
            using var stdout = System.Console.OpenStandardOutput();
            using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            writer.WriteLine(json);
            writer.Flush();
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Size");
        table.AddColumn("Description");

        var skip = (settings.Page - 1) * settings.Take;
        var displayPackages = sortedPackages.Skip(skip).Take(settings.Take).ToList();

        foreach (var pkg in displayPackages)
        {
            table.AddRow(
                pkg.Name,
                pkg.Version,
                FormatSize(pkg.Size),
                pkg.Description.EscapeMarkup().Truncate(50)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Total: {displayPackages.Count} packages[/]");
        return 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static int HandleUiModeListInstalled(ListSettings settings)
    {
        using var manager = new AlpmManager();
        manager.Initialize(true);

        var packages = manager.GetInstalledPackages();

        // Apply filter if specified
        if (!string.IsNullOrWhiteSpace(settings.Filter))
        {
            packages = packages.Where(p => p.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply sorting based on settings
        var sortedPackages = settings.Sort switch
        {
            SortOption.Size => settings.Order == SortDirection.Ascending
                ? packages.OrderBy(p => p.Size)
                : packages.OrderByDescending(p => p.Size),
            SortOption.Popularity => settings.Order == SortDirection.Ascending
                ? packages.OrderBy(p => p.Name)
                : packages.OrderByDescending(p => p.Name),
            _ => settings.Order == SortDirection.Ascending
                ? packages.OrderBy(p => p.Name)
                : packages.OrderByDescending(p => p.Name)
        };

        if (settings.JsonOutput)
        {
            var sortedList = sortedPackages.ToList();
            var json = JsonSerializer.Serialize(sortedList, ShellyCLIJsonContext.Default.ListAlpmPackageDto);
            using var stdout = Console.OpenStandardOutput();
            using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            writer.WriteLine(json);
            writer.Flush();
            return 0;
        }

        var skip = (settings.Page - 1) * settings.Take;
        var displayPackages = sortedPackages.Skip(skip).Take(settings.Take).ToList();

        foreach (var pkg in displayPackages)
        {
            Console.WriteLine($"{pkg.Name} {pkg.Version} {FormatSize(pkg.Size)} - {pkg.Description}");
        }

        Console.Error.WriteLine($"Total: {displayPackages.Count} packages");
        return 0;
    }
}