using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class PackageSettings : DefaultSettings
{
    [CommandArgument(0, "<packages>")]
    [Description("Package name(s) to operate on")]
    public string[] Packages { get; set; } = [];

    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompt")]
    public bool NoConfirm { get; set; }
}

public class InstallCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
        }

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
            if (settings.NoConfirm)
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

        AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
        manager.InstallPackages(packageList);

        AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
        return 0;
    }
}

public class RemoveCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
        }

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

        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            if (settings.NoConfirm)
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

        AnsiConsole.MarkupLine("[yellow]Initializing ALPM...[/]");
        manager.Initialize(true);

        AnsiConsole.MarkupLine("[yellow]Removing packages...[/]");
        manager.RemovePackages(packageList);

        AnsiConsole.MarkupLine("[green]Packages removed successfully![/]");
        return 0;
    }
}

public class UpdateCommand : Command<PackageSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PackageSettings settings)
    {
        if (Environment.UserName != "root")
        {
            AnsiConsole.MarkupLine("[red]Error: This operation requires root privileges. Please run with sudo.[/]");
            return 1;
        }

        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No packages specified[/]");
            return 1;
        }

        var packageList = settings.Packages.ToList();
        bool updateAll = packageList.Count == 1 && packageList[0].Equals("all", StringComparison.OrdinalIgnoreCase);

        if (updateAll)
        {
            AnsiConsole.MarkupLine("[yellow]Updating all packages (System Upgrade)...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Packages to update:[/] {string.Join(", ", packageList)}");
        }

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
            if (settings.NoConfirm)
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

        if (updateAll)
        {
            AnsiConsole.MarkupLine("[yellow]Performing full system upgrade...[/]");
            manager.UpdateAll();
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Updating packages...[/]");
            manager.UpdatePackages(packageList);
        }

        AnsiConsole.MarkupLine("[green]Packages updated successfully![/]");
        return 0;
    }
}
