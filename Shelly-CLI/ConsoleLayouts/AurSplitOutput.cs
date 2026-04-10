using PackageManager.Alpm;
using PackageManager.Aur;
using Shelly_CLI.Utility;
using Spectre.Console;

namespace Shelly_CLI.ConsoleLayouts;

public static class AurSplitOutput
{
    public static async Task<bool> Output(AurPackageManager manager, Func<AurPackageManager, Task> operation,
        bool noConfirm = false,
        int consoleRatio = 3,
        int progressRatio = 2)
    {
        var consoleLines = new List<string>();
        var progressLines = new List<string>();
        var maxVisibleLines = Console.WindowHeight - 4;

        var layout = new Layout("Columns")
            .SplitColumns(new Layout("Console").Ratio(consoleRatio),
                new Layout("Progress").Ratio(progressRatio));

        layout["Console"].Update(new Panel(new Rows()).Header("Console").Expand());
        layout["Progress"].Update(new Panel("Waiting...").Header("Progress").Expand());
        LiveDisplayContext? liveCtx = null;
        object renderLock = new();
        bool hadError = false;

        // Track package progress lines by package name for in-place updates
        var packageProgressIndex = new Dictionary<string, int>();

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

            var line =
                $"[{statusColor}][[{args.CurrentIndex}/{args.TotalCount}]] {args.PackageName.EscapeMarkup()}: {args.Status}[/]" +
                (args.Message != null ? $" - {args.Message.EscapeMarkup()}" : "");

            lock (renderLock)
            {
                if (packageProgressIndex.TryGetValue(args.PackageName, out var idx))
                {
                    progressLines[idx] = line;
                }
                else
                {
                    progressLines.Add(line);
                    packageProgressIndex[args.PackageName] = progressLines.Count - 1;
                }

                var visible = progressLines.Skip(Math.Max(0, progressLines.Count - maxVisibleLines)).ToList();
                layout["Progress"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Progress")
                        .Expand());
                liveCtx?.Refresh();
            }
        };

        manager.Progress += (sender, e) =>
        {
            var name = e.PackageName ?? "unknown";
            var pct = e.Percent ?? 0;
            var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
            var actionType = e.ProgressType;

            var line =
                $"({e.Current}/{e.HowMany}) {actionType} [bold]{name.EscapeMarkup()}[/] [green]{bar}[/] {pct,3}%";

            lock (renderLock)
            {
                if (progressLines.Count > 0 && progressLines[^1].Contains(name.EscapeMarkup()))
                {
                    progressLines[^1] = line;
                }
                else
                {
                    progressLines.Add(line);
                }

                var visible = progressLines.Skip(Math.Max(0, progressLines.Count - maxVisibleLines)).ToList();
                layout["Progress"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Progress")
                        .Expand());
                liveCtx?.Refresh();
            }
        };

        manager.Question += (sender, e) =>
        {
            if (noConfirm)
            {
                QuestionHandler.HandleQuestion(e, noConfirm: true);
                return;
            }

            switch (e.QuestionType)
            {
                case AlpmQuestionType.SelectProvider:
                    SplitOutputHelpers.HandleProviderInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx,
                        renderLock);
                    return;
                case AlpmQuestionType.SelectOptionalDeps:
                    SplitOutputHelpers.HandleOptionalDepsInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx,
                        renderLock);
                    return;
                case AlpmQuestionType.InstallIgnorePkg:
                case AlpmQuestionType.ReplacePkg:
                case AlpmQuestionType.ConflictPkg:
                case AlpmQuestionType.CorruptedPkg:
                case AlpmQuestionType.ImportKey:
                case AlpmQuestionType.RemovePkgs:
                default:
                    SplitOutputHelpers.HandleYesNoInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx, renderLock);
                    break;
            }
        };

        manager.PkgbuildDiffRequest += (sender, args) =>
        {
            if (noConfirm)
            {
                args.ProceedWithUpdate = true;
                return;
            }

            lock (renderLock)
            {
                consoleLines.Add(
                    $"[yellow bold]PKGBUILD changed for {args.PackageName.EscapeMarkup()}[/]");
                consoleLines.Add("[green]V[/] = View diff  |  [yellow]S[/] = Skip diff");

                var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                layout["Console"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Console").Expand());
                liveCtx?.Refresh();
            }

            // Wait for user to choose whether to view diff
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.V)
                {
                    lock (renderLock)
                    {
                        consoleLines.Add("[blue]--- Old PKGBUILD ---[/]");
                        consoleLines.AddRange(args.OldPkgbuild.Split('\n')
                            .Select(line => line.TrimEnd('\r').EscapeMarkup()));
                        consoleLines.Add("[blue]--- New PKGBUILD ---[/]");
                        consoleLines.AddRange(args.NewPkgbuild.Split('\n')
                            .Select(line => line.TrimEnd('\r').EscapeMarkup()));

                        var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        liveCtx?.Refresh();
                    }

                    break;
                }

                if (key.Key == ConsoleKey.S)
                {
                    lock (renderLock)
                    {
                        consoleLines.Add("[dim]Diff skipped.[/]");
                        var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        liveCtx?.Refresh();
                    }

                    break;
                }
            }

            // Now ask whether to proceed
            lock (renderLock)
            {
                consoleLines.Add(
                    $"[yellow bold]Proceed with update for {args.PackageName.EscapeMarkup()}?[/]");
                consoleLines.Add("[green]Y[/] = Yes  |  [red]N[/] = No");

                var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                layout["Console"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Console").Expand());
                liveCtx?.Refresh();
            }

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Y)
                {
                    lock (renderLock)
                    {
                        consoleLines.Add("[green]> Proceeding with update.[/]");
                        var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        liveCtx?.Refresh();
                    }

                    args.ProceedWithUpdate = true;
                    break;
                }

                if (key.Key == ConsoleKey.N)
                {
                    lock (renderLock)
                    {
                        consoleLines.Add("[red]> Update skipped.[/]");
                        var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        liveCtx?.Refresh();
                    }

                    args.ProceedWithUpdate = false;
                    break;
                }
            }
        };

        manager.BuildOutput += (sender, e) =>
        {
            lock (renderLock)
            {
                if (e.Percent.HasValue)
                {
                    var prefix = $"[bold]{e.PackageName.EscapeMarkup()}[/] ";
                    var barLength = 20;
                    var filled = (int)(barLength * e.Percent.Value / 100.0);
                    var bar = new string('█', filled) + new string('░', barLength - filled);
                    var line =
                        $"{prefix}[yellow]{bar} {e.Percent.Value}%[/] {(e.ProgressMessage ?? "").EscapeMarkup()}";

                    var existingIdx =
                        consoleLines.FindLastIndex(l => l.Contains(e.PackageName.EscapeMarkup()) && l.Contains('█'));
                    if (existingIdx >= 0)
                    {
                        consoleLines[existingIdx] = line;
                    }
                    else
                    {
                        consoleLines.Add(line);
                    }
                }
                else
                {
                    var color = e.IsError ? "red" : "dim";
                    consoleLines.Add($"[{color}]{e.Line.EscapeMarkup()}[/]");
                }
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.PackageOperation += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add(string.IsNullOrWhiteSpace(e.PackageName)
                    ? $"{e.EventType}".EscapeMarkup()
                    : $"{e.EventType} for {e.PackageName}".EscapeMarkup());
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.ScriptletInfo += (sender, e) =>
        {
            lock (renderLock)
            {
                var line = e.Line ?? string.Empty;
                consoleLines.Add(string.IsNullOrEmpty(line)
                    ? "Running scriptlet..."
                    : $"Scriptlet: {line.EscapeMarkup()}");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.HookRun += (sender, e) =>
        {
            lock (renderLock)
            {
                var line = e.Description ?? string.Empty;
                consoleLines.Add(string.IsNullOrEmpty(line)
                    ? "Running hook..."
                    : $"Hook: {line.EscapeMarkup()}");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.Replaces += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add(
                    $"{e.Repository.EscapeMarkup()}/{e.PackageName.EscapeMarkup()} replaces {string.Join(",", e.Replaces.Select(r => r.EscapeMarkup()))} packages");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.PacnewInfo += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add($"Pacnew stored @ {e.FileLocation.EscapeMarkup()}.pacnew");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.PacsaveInfo += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add($"Pacsave stored @ {e.FileLocation.EscapeMarkup()}.pacsave");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.ErrorEvent += (sender, e) =>
        {
            lock (renderLock)
            {
                hadError = true;
                consoleLines.Add($"[red]ERROR: {e.Error.EscapeMarkup()}[/]");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        await AnsiConsole.Live(layout).StartAsync(async ctx =>
        {
            liveCtx = ctx;
            await operation(manager);
        });
        return !hadError;
    }
}