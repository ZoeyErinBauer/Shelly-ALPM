using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class CorruptedPackages : Command<CorruptedPackagesSettings>
{
    public override int Execute(CommandContext context, CorruptedPackagesSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiMode(settings);
        }

        RootElevator.EnsureRootExectuion();
        AnsiConsole.MarkupLine("[yellow] Initializing ALPM... [/]");
        using var manager = new AlpmManager();
        manager.Initialize(true);
        var results = manager.RemoveCorruptedPackages(settings.DryRun);
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[green] No corrupted packages found! [/]");
            return 0;
        }

        AnsiConsole.MarkupLine(settings.DryRun ? "[green] Running would remove: [/]" : "[green] Removed: [/]");

        var third = (int)Math.Ceiling(results.Count / 3.0);
        var columnOne = results.Take(third).ToList();
        var columnTwo = results.Skip(third).Take(third).ToList();
        var columnThree = results.Skip(third * 2).ToList();

        var table = new Table();
        if (columnOne.Count > 0) table.AddColumn("Package");
        if (columnTwo.Count > 0) table.AddColumn("Package");
        if (columnThree.Count > 0) table.AddColumn("Package");
        var length = Math.Max(columnOne.Count, Math.Max(columnTwo.Count, columnThree.Count));
        for (var i = 0; i < length; i++)
        {
            var columnOneOutput = i < columnOne.Count ? columnOne[i] : "";
            var columnTwoOutput = i < columnTwo.Count ? columnTwo[i] : "";
            var columnThreeOutput = i < columnThree.Count ? columnThree[i] : "";
            table.AddRow(columnOneOutput, columnTwoOutput, columnThreeOutput);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private int HandleUiMode(CorruptedPackagesSettings settings)
    {
        using var manager = new AlpmManager();
        manager.Initialize(true);
        var results = manager.RemoveCorruptedPackages(settings.DryRun);
        Console.WriteLine(string.Join(",", results));
        return 0;
    }
}