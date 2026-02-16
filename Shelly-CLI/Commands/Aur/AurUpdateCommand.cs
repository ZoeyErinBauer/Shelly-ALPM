using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using PackageManager.Aur;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpdateCommand : AsyncCommand<AurPackageSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context,
        [NotNull] AurPackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No packages specified.[/]");
            return 1;
        }

        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true);

            manager.PackageProgress += (sender, args) =>
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
            };

            manager.Progress += (sender, args) =>
            {
                AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
            };

            manager.Question += (sender, args) =>
            {
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
                                Console.Error.WriteLine($"[Shelly][ALPM_CONFLICT]{args.QuestionText}");
                                for (int i = 0; i < args.ProviderOptions.Count; i++)
                                {
                                    Console.Error.WriteLine($"[Shelly][ALPM_CONFLICT_OPTION]{i}:{args.ProviderOptions[i]}");
                                }

                                Console.Error.WriteLine("[Shelly][ALPM_CONFLICT_END]");
                            }
                            else
                            {
                                // Machine-readable format for UI integration
                                Console.Error.WriteLine($"[Shelly][ALPM_SELECT_PROVIDER]{args.DependencyName}");
                                for (int i = 0; i < args.ProviderOptions.Count; i++)
                                {
                                    Console.Error.WriteLine($"[Shelly][ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                                }

                                Console.Error.WriteLine("[Shelly][ALPM_PROVIDER_END]");
                            }
                            Console.Error.Flush();
                            var input = Console.ReadLine();
                            args.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
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
                        Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                        Console.Error.Flush();
                        var input = Console.ReadLine();
                        args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
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
            };

            manager.PkgbuildDiffRequest += (sender, args) =>
            {
                if (settings.NoConfirm)
                {
                    args.ProceedWithUpdate = true;
                    return;
                }

                var showDiff = AnsiConsole.Confirm(
                    $"[yellow]PKGBUILD changed for {args.PackageName}. View diff?[/]", defaultValue: false);

                if (showDiff)
                {
                    AnsiConsole.MarkupLine("[blue]--- Old PKGBUILD ---[/]");
                    AnsiConsole.WriteLine(args.OldPkgbuild);
                    AnsiConsole.MarkupLine("[blue]--- New PKGBUILD ---[/]");
                    AnsiConsole.WriteLine(args.NewPkgbuild);
                }

                args.ProceedWithUpdate = AnsiConsole.Confirm(
                    $"[yellow]Proceed with update for {args.PackageName}?[/]", defaultValue: true);
            };

            AnsiConsole.MarkupLine($"[yellow]Updating AUR packages: {string.Join(", ", settings.Packages)}[/]");
            await manager.UpdatePackages(settings.Packages.ToList());
            AnsiConsole.MarkupLine("[green]Update complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Update failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}