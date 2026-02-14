using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class RemoveCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]Packages to remove:[/] {string.Join(", ", packageList)}");

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        using var manager = new AlpmManager();
        object renderLock = new();

        manager.Question += (sender, args) =>
        {
            lock (renderLock)
            {
                AnsiConsole.WriteLine();
                // Handle SelectProvider differently - it needs a selection, not yes/no
                if (args.QuestionType == AlpmQuestionType.SelectProvider && args.ProviderOptions?.Count > 0)
                {
                    if (settings.NoConfirm)
                    {
                        // Machine-readable format for UI integration
                        Console.Error.WriteLine($"[Shelly][ALPM_SELECT_PROVIDER]{args.DependencyName}");
                        for (int i = 0; i < args.ProviderOptions.Count; i++)
                        {
                            Console.Error.WriteLine($"[Shelly][ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                        }
                        Console.Error.Flush();
                        var input = Console.ReadLine();
                        args.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
                    }
                    else
                    {
                        var selection = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title($"[yellow]{args.QuestionText}[/]")
                                .AddChoices(args.ProviderOptions));
                        args.Response = args.ProviderOptions.IndexOf(selection);
                    }
                }
                else if (settings.NoConfirm)
                {
                    // Machine-readable format for UI integration
                    Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                    Console.Error.Flush();
                    var input = Console.ReadLine();
                    args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
                }
                else
                {
                    var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                    args.Response = response ? 1 : 0;
                }
                
            }
        };

        AnsiConsole.MarkupLine("[yellow]Initializing ALPM...[/]");
        manager.Initialize(true);

        AnsiConsole.MarkupLine("[yellow]Removing packages...[/]");
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
                manager.RemovePackages(packageList);
            });

        AnsiConsole.MarkupLine("[green]Packages removed successfully![/]");
        return 0;
    }
}
