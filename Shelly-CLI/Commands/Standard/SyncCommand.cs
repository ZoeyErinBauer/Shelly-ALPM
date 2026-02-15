using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class SyncCommand : Command<SyncSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] SyncSettings settings)
    {
        using var manager = new AlpmManager();
        object renderLock = new();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Initializing ALPM...", ctx => { manager.Initialize(true); });

        AnsiConsole.MarkupLine("[yellow]Synchronizing package databases...[/]");
        var progressTable = new Table().AddColumns("Package", "Progress", "Status", "Stage");
        AnsiConsole.Live(progressTable).AutoClear(false)
            .Start(ctx =>
            {
                var rowIndex = new Dictionary<string, int>();

                manager.Progress += (sender, args) =>
                {
                    lock (renderLock)
                    {
                        var name = args.PackageName ?? "unknown";
                        var pct = args.Percent ?? 0;
                        var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
                        var actionType = args.ProgressType;

                        if (!rowIndex.TryGetValue(name, out var idx))
                        {
                            progressTable.AddRow(
                                $"[blue]{Markup.Escape(name)}[/]",
                                $"[green]{bar}[/]",
                                $"{pct}%",
                                $"{actionType}"
                            );
                            rowIndex[name] = rowIndex.Count;
                        }
                        else
                        {
                            progressTable.UpdateCell(idx, 1, $"[green]{bar}[/]");
                            progressTable.UpdateCell(idx, 2, $"{pct}%");
                            progressTable.UpdateCell(idx, 3, $"{actionType}");
                        }

                        ctx.Refresh();
                    }
                };
                manager.Sync(settings.Force);
            });

        AnsiConsole.MarkupLine("[green]Package databases synchronized successfully![/]");
        return 0;
    }
}
