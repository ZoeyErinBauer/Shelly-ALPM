using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class InstallCommand : Command<InstallPackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] InstallPackageSettings settings)
    {
        if (Program.IsUiMode)
        {
            return HandleUiModeInstall(settings);
        }

        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]Packages to install:[/] {string.Join(", ", packageList)}");

        if (!AnsiConsole.Confirm("Do you want to proceed?"))
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
            return 0;
        }


        var manager = new AlpmManager();
        object renderLock = new();

        manager.Question += (sender, args) =>
        {
            lock (renderLock)
            {
                AnsiConsole.WriteLine();
                QuestionHandler.HandleQuestion(args, Program.IsUiMode, settings.NoConfirm);
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
                manager.InstallPackages(packageList);
            });

        manager.Dispose();
        AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
        return 0;
    }

    private int HandleUiModeInstall(InstallPackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            Console.WriteLine("Error: No packages specified");
            return 1;
        }

        var manager = new AlpmManager();
        manager.Question += (sender, args) => { QuestionHandler.HandleQuestion(args, true, settings.NoConfirm); };
        Console.WriteLine("Initializing and syncing ALPM...");
        manager.IntializeWithSync();
        if (settings.BuildDepsOn)
        {
            if (settings.Packages.Length > 1)
            {
                Console.WriteLine("Cannot build dependencies for multiple packages at once.");
                return -1;
            }

            if (settings.MakeDepsOn)
            {
                AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
                manager.InstallDependenciesOnly(settings.Packages.ToList().First(), true, AlpmTransFlag.None);
                return 0;
            }

            Console.WriteLine("Installing packages...");
            manager.InstallDependenciesOnly(settings.Packages.ToList().First(), false, AlpmTransFlag.None);
            Console.WriteLine("Packages installed successfully!");
            return 0;
        }

        if (settings.NoDeps)
        {
            Console.WriteLine("Skipping dependency installation.");
            Console.WriteLine("Installing packages...");
            manager.InstallPackages(settings.Packages.ToList(), AlpmTransFlag.NoDeps);
            Console.WriteLine("Packages installed successfully!");
            return 0;
        }

        Console.WriteLine("Installing packages...");
        var rowIndex = new Dictionary<string, int>();
        manager.Progress += (sender, args) => { Console.WriteLine($"{args.PackageName}: {args.Percent}%"); };
        manager.InstallPackages(settings.Packages.ToList());
        Console.WriteLine("Finished installing packages.");
        manager.Dispose();
        return 0;
    }
}