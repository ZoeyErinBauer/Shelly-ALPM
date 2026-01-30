using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class ListInstalledCommand : Command<DefaultSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] DefaultSettings settings)
    {
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

        if (settings.JsonOutput)
        {
            var json = JsonSerializer.Serialize(packages, ShellyCLIJsonContext.Default.ListAlpmPackageDto);
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

        foreach (var pkg in packages.OrderBy(p => p.Name))
        {
            table.AddRow(
                pkg.Name,
                pkg.Version,
                FormatSize(pkg.Size),
                pkg.Description.EscapeMarkup().Truncate(50)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[blue]Total: {packages.Count} packages[/]");
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
}
