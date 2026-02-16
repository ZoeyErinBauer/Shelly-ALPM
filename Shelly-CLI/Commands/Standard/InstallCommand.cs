using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class InstallCommand : Command<InstallPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] InstallPackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]Packages to install:[/] {string.Join(", ", packageList)}");

        if (!settings.NoConfirm)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        var manager = new AlpmManager();
        object renderLock = new();

        manager.Question += (sender, args) =>
        {
            lock (renderLock)
            {
                AnsiConsole.WriteLine();
                // Handle SelectProvider and ConflictPkg differently - they need a selection, not yes/no
                if ((args.QuestionType == AlpmQuestionType.SelectProvider ||
                     args.QuestionType == AlpmQuestionType.ConflictPkg) 
                    && args.ProviderOptions?.Count > 0)
                {
                    if (settings.NoConfirm)
                    {
                        if (Program.IsUiMode)
                        {
                            if (args.QuestionType == AlpmQuestionType.ConflictPkg)
                            {
                                // Dedicated conflict protocol for UI integration
                                Console.Error.WriteLine($"[ALPM_CONFLICT]{args.QuestionText}");
                                for (int i = 0; i < args.ProviderOptions.Count; i++)
                                {
                                    Console.Error.WriteLine($"[ALPM_CONFLICT_OPTION]{i}:{args.ProviderOptions[i]}");
                                }

                                Console.Error.WriteLine("[ALPM_CONFLICT_END]");
                            }
                            else
                            {
                                // Machine-readable format for UI integration
                                Console.Error.WriteLine($"[ALPM_SELECT_PROVIDER]{args.DependencyName}");
                                for (int i = 0; i < args.ProviderOptions.Count; i++)
                                {
                                    Console.Error.WriteLine($"[ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                                }

                                Console.Error.WriteLine("[ALPM_PROVIDER_END]");
                            }
                            Console.Error.Flush();
                            var input = Console.ReadLine();
                            args.SetResponse(int.TryParse(input?.Trim(), out var idx) ? idx : 0);
                        }
                        else
                        {
                            // Non-interactive CLI mode: default to the first provider
                            args.Response = 0;
                        }
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
                    if (Program.IsUiMode)
                    {
                        // Machine-readable format for UI integration
                        Console.Error.WriteLine($"[ALPM_QUESTION]{args.QuestionText}");
                        Console.Error.Flush();
                        var input = Console.ReadLine();
                        args.SetResponse(input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0);
                    }
                    else
                    {
                        // Non-interactive CLI mode: automatically confirm
                        args.Response = 1;
                    }
                }
                else
                {
                    var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                    args.Response = response ? 1 : 0;
                }
                
            }
        };

        AnsiConsole.MarkupLine("[yellow]Initializing and syncing ALPM...[/]");
        manager.IntializeWithSync();

        if (settings.BuildDepsOn)
        {
            if (settings.Packages.Length > 1)
            {
                AnsiConsole.MarkupLine("[yellow]Cannot build dependencies for multiple packages at once.[/]");
                return 0;
            }

            if (settings.MakeDepsOn)
            {
                AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
                manager.InstallDependenciesOnly(packageList.First(), true, AlpmTransFlag.None);
                return 0;
            }

            AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
            manager.InstallDependenciesOnly(packageList.First(), false, AlpmTransFlag.None);
            AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
            return 0;
        }

        if (settings.NoDeps)
        {
            AnsiConsole.MarkupLine("[yellow]Skipping dependency installation.[/]");
            AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
            manager.InstallPackages(packageList, AlpmTransFlag.NoDeps);
            AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
        var progressTable = new Table().AddColumns("Package", "Progress", "Status","Stage");
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
                manager.InstallPackages(packageList);
            });

        manager.Dispose();
        AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
        return 0;
    }
}