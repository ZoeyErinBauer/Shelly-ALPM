using System.Diagnostics.CodeAnalysis;
using PackageManager.Alpm;
using PackageManager.Aur;
using Shelly_CLI.ConsoleLayouts;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public class AurUpdateCommand : AsyncCommand<AurPackageSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context,
        [NotNull] AurPackageSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeUpdate(settings);
        }
        
        if (settings.Packages.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No packages specified.[/]");
            return 1;
        }

        RootElevator.EnsureRootExectuion();
        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true, noCheck: !settings.Check);

            AnsiConsole.MarkupLine($"[yellow]Updating AUR packages: {string.Join(", ", settings.Packages.Select(p => p.EscapeMarkup()))}[/]");
            await AurSplitOutput.Output(manager, m => m.UpdatePackages(settings.Packages.ToList()), settings.NoConfirm);
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

    private static async Task<int> HandleUiModeUpdate(AurPackageSettings settings)
    {
        if (settings.Packages.Length == 0)
        {
            Console.Error.WriteLine("No packages specified.");
            return 1;
        }

        AurPackageManager? manager = null;
        try
        {
            manager = new AurPackageManager();
            await manager.Initialize(root: true, noCheck: !settings.Check);

            manager.PackageProgress += (sender, args) =>
            {
                Console.Error.WriteLine($"[{args.CurrentIndex}/{args.TotalCount}] {args.PackageName}: {args.Status}" +
                    (args.Message != null ? $" - {args.Message}" : ""));
            };

            manager.Progress += (sender, args) =>
            {
                Console.Error.WriteLine($"{args.PackageName}: {args.Percent}%");
            };

            manager.Question += (sender, args) =>
            {
                QuestionHandler.HandleQuestion(args, true, settings.NoConfirm);
            };

            manager.BuildOutput += (sender, e) =>
            {
                if (e.IsError)
                    Console.Error.WriteLine($"[Shelly] makepkg error: {e.Line}");
                else if (e.Percent.HasValue)
                    Console.Error.WriteLine($"[AUR_PROGRESS]Percent: {e.Percent}% Message: {e.ProgressMessage}");
                else
                    Console.Error.WriteLine($"[Shelly] makepkg: {e.Line}");
            };

            manager.PkgbuildDiffRequest += (sender, args) =>
            {
                if (settings.NoConfirm)
                {
                    args.ProceedWithUpdate = true;
                    return;
                }

                Console.Error.WriteLine($"PKGBUILD changed for {args.PackageName}.");
                Console.Error.WriteLine("--- Old PKGBUILD ---");
                Console.Error.WriteLine(args.OldPkgbuild);
                Console.Error.WriteLine("--- New PKGBUILD ---");
                Console.Error.WriteLine(args.NewPkgbuild);
                args.ProceedWithUpdate = true;
            };

            Console.Error.WriteLine($"Updating AUR packages: {string.Join(", ", settings.Packages)}");
            await manager.UpdatePackages(settings.Packages.ToList());
            Console.Error.WriteLine("Update complete.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update failed: {ex.Message}");
            return 1;
        }
        finally
        {
            manager?.Dispose();
        }
    }
}