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

        using var manager = new AlpmManager();

        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            // Handle SelectProvider differently - it needs a selection, not yes/no
            if (args.QuestionType == AlpmQuestionType.SelectProvider && args.ProviderOptions?.Count > 0)
            {
                if (settings.NoConfirm)
                {
                    // Machine-readable format for UI integration
                    Console.Error.WriteLine($"[ALPM_SELECT_PROVIDER]{args.DependencyName}");
                    for (int i = 0; i < args.ProviderOptions.Count; i++)
                    {
                        Console.Error.WriteLine($"[ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                    }
                    Console.Error.WriteLine("[ALPM_PROVIDER_OPTION_END]");
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
        manager.InstallPackages(packageList);

        AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
        return 0;
    }
}