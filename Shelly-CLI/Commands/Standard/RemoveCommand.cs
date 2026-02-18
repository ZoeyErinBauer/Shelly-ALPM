using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using Shelly_CLI.Utility;
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

        if (!Program.IsUiMode)
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
                QuestionHandler.HandleQuestion(args, Program.IsUiMode, settings.NoConfirm);
            }
        };

        AnsiConsole.MarkupLine("[yellow]Initializing ALPM...[/]");
        manager.Initialize(true);

        AnsiConsole.MarkupLine("[yellow]Removing packages...[/]");

        int currentPkgIndex = 0;
        int totalPkgs = packageList.Count;
        string? lastPackageName = null;
        int lastPercent = 0;

        manager.Progress += (sender, args) =>
        {
            lock (renderLock)
            {
                var name = args.PackageName ?? "unknown";
                var pct = args.Percent ?? 0;
                var bar = string.Join("", Enumerable.Repeat("🐚 ", pct * 2 / 5)) + new string('░', 20 - pct / 5);
                var actionType = args.ProgressType;

                // Detect package change
                if (name != lastPackageName)
                {
                    // If this isn't the first package, complete the previous line
                    if (lastPackageName != null)
                    {
                        Console.WriteLine(); // Move to new line
                        currentPkgIndex++;
                    }

                    lastPackageName = name;
                    lastPercent = 0;
                }

                // Update current line with carriage return
                Console.Write(
                    $"\r({currentPkgIndex + 1}/{totalPkgs}) installing {name,-40}  [{bar}] {pct,3}% - {actionType,-20}");

                lastPercent = pct;
            }
        };
        manager.RemovePackages(packageList);


        AnsiConsole.MarkupLine("[green]Packages removed successfully![/]");
        return 0;
    }
}