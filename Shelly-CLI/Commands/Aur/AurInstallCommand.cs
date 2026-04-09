using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PackageManager.Alpm;
using PackageManager.Aur;
using Shelly_CLI.ConsoleLayouts;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurInstallCommand : AsyncCommand<AurInstallSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context,
        [NotNull] AurInstallSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeInstall(settings);
        }

        AurPackageManager? manager = null;
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No packages specified.[/]");
            return 1;
        }
        RootElevator.EnsureRootExectuion();
        var packageList = settings.Packages.ToList();

        AnsiConsole.MarkupLine($"[yellow]AUR packages to install:[/] {string.Join(", ", packageList.Select(p => p.EscapeMarkup()))}");

        if (!Program.IsUiMode)
        {
            if (!AnsiConsole.Confirm("Do you want to proceed?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }
        }

        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true, useChroot: settings.UseChroot, noCheck: !settings.Check);

            if (settings.BuildDepsOn)
            {
                if (settings.Packages.Length > 1)
                {
                    AnsiConsole.MarkupLine("[yellow]Cannot build dependencies for multiple packages at once.[/]");
                    return 0;
                }

                if (settings.MakeDepsOn)
                {
                    AnsiConsole.MarkupLine("[yellow]Installing dependencies (including make dependencies)...[/]");
                    await AurSplitOutput.Output(manager, m => m.InstallDependenciesOnly(packageList.First(), true), settings.NoConfirm);
                    AnsiConsole.MarkupLine("[green]Dependencies installed successfully![/]");
                    return 0;
                }

                AnsiConsole.MarkupLine("[yellow]Installing dependencies...[/]");
                await AurSplitOutput.Output(manager, m => m.InstallDependenciesOnly(packageList.First(), false), settings.NoConfirm);
                AnsiConsole.MarkupLine("[green]Dependencies installed successfully![/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]Installing AUR packages: {string.Join(", ", settings.Packages.Select(p => p.EscapeMarkup()))}[/]");
            await AurSplitOutput.Output(manager, m => m.InstallPackages(packageList), settings.NoConfirm);

            manager.Dispose();
            manager = new AurPackageManager();
            await manager.Initialize(root: true, useChroot: settings.UseChroot, noCheck: !settings.Check);
            var packageNames = packageList.Select(x => x.EndsWith("-bin") ? x.Split("-")[0] : x).ToList();
            var missingPackages = await GetMissingPackages(manager, packageNames);
            if (missingPackages.Count > 0)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Installation failed:[/] {string.Join(", ", missingPackages.Select(p => p.EscapeMarkup()))}");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]Installation complete.[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Installation failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }

    private static async Task<int> HandleUiModeInstall(AurInstallSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            Console.Error.WriteLine("Error: No packages specified");
            return 1;
        }

        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true, useChroot: settings.UseChroot, noCheck: !settings.Check);

            var packageList = settings.Packages.ToList();

            // Handle package progress events
            manager.PackageProgress += (sender, args) =>
            {
                Console.Error.WriteLine($"[{args.CurrentIndex}/{args.TotalCount}] {args.PackageName}: {args.Status}" +
                                        (args.Message != null ? $" - {args.Message}" : ""));
            };

            // Handle progress events
            manager.Progress += (sender, args) => { Console.Error.WriteLine($"{args.PackageName}: {args.Percent}%"); };

            // Handle questions
            manager.Question += (sender, args) => { QuestionHandler.HandleQuestion(args, true, settings.NoConfirm); };

            // Handle build output
            manager.BuildOutput += (sender, e) =>
            {
                if (e.IsError)
                    Console.Error.WriteLine($"[Shelly] makepkg error: {e.Line}");
                else if (e.Percent.HasValue)
                    Console.Error.WriteLine($"[AUR_PROGRESS]Percent: {e.Percent}% Message: {e.ProgressMessage}");
                else
                    Console.Error.WriteLine($"[Shelly] makepkg: {e.Line}");
            };

            // Handle build dependencies only mode
            if (settings.BuildDepsOn)
            {
                if (settings.Packages.Length > 1)
                {
                    Console.Error.WriteLine("Cannot build dependencies for multiple packages at once.");
                    return 1;
                }

                if (settings.MakeDepsOn)
                {
                    Console.Error.WriteLine("Installing dependencies (including make dependencies)...");
                    await manager.InstallDependenciesOnly(packageList.First(), true);
                    Console.Error.WriteLine("Dependencies installed successfully!");
                    return 0;
                }

                Console.Error.WriteLine("Installing dependencies...");
                await manager.InstallDependenciesOnly(packageList.First(), false);
                Console.Error.WriteLine("Dependencies installed successfully!");
                return 0;
            }

            Console.Error.WriteLine($"Installing AUR packages: {string.Join(", ", packageList)}");
            await manager.InstallPackages(packageList);

            // Recreate manager to get fresh installed package list (avoid stale cache)
            manager.Dispose();
            manager = new AurPackageManager();
            await manager.Initialize(root: true, useChroot: settings.UseChroot, noCheck: !settings.Check);

            var missingPackages = await GetMissingPackages(manager, packageList);
            if (missingPackages.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Installation failed: Failed to install AUR package(s): {string.Join(", ", missingPackages)}");
                return 1;
            }

            Console.Error.WriteLine("Installation complete.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Installation failed: {ex.Message}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }

    private static async Task<List<string>> GetMissingPackages(AurPackageManager manager, List<string> packageList)
    {
        var installedPackages = await manager.GetInstalledPackages();
        var installedPackageNames = installedPackages
            .Select(package => package.Name)
            .ToHashSet(StringComparer.Ordinal);

        return packageList
            .Where(packageName => !installedPackageNames.Contains(packageName))
            .ToList();
    }
}