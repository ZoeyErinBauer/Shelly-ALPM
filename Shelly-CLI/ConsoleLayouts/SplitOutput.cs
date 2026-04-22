using PackageManager.Alpm;
using Shelly_CLI.Configuration;
using Shelly_CLI.Utility;
using Spectre.Console;

namespace Shelly_CLI.ConsoleLayouts;

public static class SplitOutput
{
    private sealed class BarState
    {
        public string Name = "";
        public ulong Current;
        public ulong HowMany;
        public int Pct;
        public string ActionType = "";
        public bool Completed;
    }

    public static async Task<bool> Output(IAlpmManager manager, Func<IAlpmManager, Task<bool>> operation, bool noConfirm = false,
        int consoleRation = 3,
        int progressRatio = 2)
    {
        var cfg = ConfigManager.ReadConfig();
        var style = ProgressBarRenderer.ParseStyle(cfg.ProgressBarStyle);
        var fps = Math.Clamp(cfg.ProgressBarFps, 1, 30);
        var barWidth = cfg.ProgressBarWidth;

        var consoleLines = new List<string>();
        var progressLines = new List<string>();
        var pendingPacfiles = new List<PendingPacfile>();
        var pacfileLock = new object();
        var maxVisibleLines = Console.WindowHeight - 4; // adjust as needed
        var visible = consoleLines
            .Skip(Math.Max(0, consoleLines.Count - maxVisibleLines))
            .Select(l => new Markup(l))
            .ToList();

        var layout = new Layout("Columns")
            .SplitColumns(new Layout("Console").Ratio(consoleRation),
                new Layout("Progress").Ratio(progressRatio));

        layout["Console"].Update(new Panel(new Rows(visible)).Header("Console").Expand());
        layout["Progress"].Update(new Panel("Waiting...").Header("Progress").Expand());
        LiveDisplayContext? liveCtx = null;
        object renderLock = new();

        var rows = new Dictionary<string, BarState>(StringComparer.Ordinal);
        var order = new List<string>();

        string RenderLine(BarState r, int frame)
        {
            var bar = ProgressBarRenderer.Render(r.Pct, frame, style, barWidth);
            return $"({r.Current}/{r.HowMany}) {r.ActionType} " +
                   $"[bold]{r.Name.EscapeMarkup()}[/] {bar} {r.Pct,3}%";
        }

        void RebuildProgressLines(int frame)
        {
            progressLines.Clear();
            foreach (var key in order)
            {
                progressLines.Add(RenderLine(rows[key], frame));
            }
        }

        manager.Progress += (sender, e) =>
        {
            var name = e.PackageName ?? "unknown";
            var pct = e.Percent ?? 0;
            var actionType = e.ProgressType.ToString();

            lock (renderLock)
            {
                if (!rows.TryGetValue(name, out var r))
                {
                    r = new BarState { Name = name };
                    rows[name] = r;
                    order.Add(name);
                }

                r.Current = e.Current ?? 0;
                r.HowMany = e.HowMany ?? 0;
                r.Pct = pct;
                r.ActionType = actionType;
                if (pct >= 100) r.Completed = true;

                RebuildProgressLines(frame: 0);
            }

            SplitOutputHelpers.UpdatePanel(layout, "Progress", progressLines, maxVisibleLines, renderLock, liveCtx);
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
                consoleLines.Add($"{e.Repository.EscapeMarkup()}/{e.PackageName.EscapeMarkup()} replaces {string.Join(",", e.Replaces.Select(r => r.EscapeMarkup()))} packages");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.PacnewInfo += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add($"Pacnew stored @ {e.FileLocation.EscapeMarkup()}.pacnew");
            }

            lock (pacfileLock)
            {
                pendingPacfiles.Add(new PendingPacfile(PacfileKind.Pacnew, null, e.FileLocation + ".pacnew", DateTime.UtcNow));
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };
        
        manager.PacsaveInfo += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add($"Pacsave stored @ {e.FileLocation.EscapeMarkup()}.pacsave");
            }

            lock (pacfileLock)
            {
                pendingPacfiles.Add(new PendingPacfile(PacfileKind.Pacsave, e.OldPackage, e.FileLocation + ".pacsave", DateTime.UtcNow));
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.ErrorEvent += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add($"[red]ERROR: {e.Error.EscapeMarkup()}[/]");
            }

            SplitOutputHelpers.UpdatePanel(layout, "Console", consoleLines, maxVisibleLines, renderLock, liveCtx);
        };

        manager.Question += (sender, e) =>
        {
            if (noConfirm)
            {
                QuestionHandler.HandleQuestion(e, noConfirm: true);
                return;
            }

            if (e.QuestionType == AlpmQuestionType.SelectProvider)
            {
                SplitOutputHelpers.HandleProviderInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx, renderLock);
                return;
            }

            if (e.QuestionType == AlpmQuestionType.SelectOptionalDeps)
            {
                SplitOutputHelpers.HandleOptionalDepsInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx, renderLock);
                return;
            }

            SplitOutputHelpers.HandleYesNoInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx, renderLock);
        };

        bool result = false;
        await AnsiConsole.Live(layout).StartAsync(async ctx =>
        {
            liveCtx = ctx;

            if (!ProgressBarRenderer.ShouldAnimate(style))
            {
                result = await operation(manager);
                return;
            }

            using var cts = new CancellationTokenSource();
            var delay = TimeSpan.FromMilliseconds(1000.0 / fps);

            var ticker = Task.Run(async () =>
            {
                int frame = 0;
                while (!cts.IsCancellationRequested)
                {
                    frame++;
                    lock (renderLock)
                    {
                        RebuildProgressLines(frame);
                    }

                    SplitOutputHelpers.UpdatePanel(layout, "Progress", progressLines,
                        maxVisibleLines, renderLock, liveCtx);

                    try { await Task.Delay(delay, cts.Token); }
                    catch (TaskCanceledException) { break; }
                }
            }, cts.Token);

            try
            {
                result = await operation(manager);
            }
            finally
            {
                cts.Cancel();
                try { await ticker; } catch { }
            }
        });

        try
        {
            await PacfileFlusher.FlushAsync(pendingPacfiles, pacfileLock);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]warning:[/] failed to store pacfiles: {ex.Message.EscapeMarkup()}");
        }

        return result;
    }
}
