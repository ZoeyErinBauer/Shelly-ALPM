using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using PackageManager.Aur;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurRemoveCommand : AsyncCommand<AurPackageSettings>
{
    //TODO:  implement isuimode support
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] AurPackageSettings settings)
    {
        AurPackageManager? manager = null;
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No packages specified.[/]");
            return 1;
        }

        try
        {
            
            manager = new AurPackageManager();
            await manager.Initialize(root: true);
            object renderLock = new();

            manager.PackageProgress += (sender, args) =>
            {
                lock (renderLock)
                {
                    var statusColor = args.Status switch
                    {
                        PackageProgressStatus.Downloading => "yellow",
                        PackageProgressStatus.Building => "blue",
                        PackageProgressStatus.Installing => "cyan",
                        PackageProgressStatus.Completed => "green",
                        PackageProgressStatus.Failed => "red",
                        _ => "white"
                    };

                    AnsiConsole.MarkupLine(
                        $"[{statusColor}][[{args.CurrentIndex}/{args.TotalCount}]] {args.PackageName}: {args.Status}[/]" +
                        (args.Message != null ? $" - {args.Message.EscapeMarkup()}" : ""));
                }
            };

            manager.Question += (sender, args) =>
            {
                lock (renderLock)
                {
                    AnsiConsole.WriteLine();
                    // Handle SelectProvider and ConflictPkg differently - they need a selection, not yes/no
                    QuestionHandler.HandleQuestion(args,Program.IsUiMode,settings.NoConfirm);
                }
            };

            AnsiConsole.MarkupLine($"[yellow]Removing AUR packages: {string.Join(", ", settings.Packages)}[/]");
            var progressTable = new Table().AddColumns("Package", "Progress", "Status", "Stage");
            await AnsiConsole.Live(progressTable).AutoClear(false)
                .StartAsync(async ctx =>
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
                    await manager.RemovePackages(settings.Packages.ToList());
                });
            AnsiConsole.MarkupLine("[green]Removal complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Removal failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}
