using PackageManager.Alpm;
using Shelly_CLI.Utility;
using Spectre.Console;

namespace Shelly_CLI.ConsoleLayouts;

public static class SplitOutput
{
    public static async Task<bool> Output(IAlpmManager manager, Func<IAlpmManager, Task<bool>> operation, bool noConfirm = false,
        int consoleRation = 3,
        int progressRatio = 2)
    {
        var consoleLines = new List<string>();
        var progressLines = new List<string>();
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
        manager.Progress += (sender, e) =>
        {
            var name = e.PackageName ?? "unknown";
            var pct = e.Percent ?? 0;
            var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
            var actionType = e.ProgressType;

            var line =
                $"({e.Current}/{e.HowMany}) {actionType} [bold]{name.EscapeMarkup()}[/] [green]{bar}[/] {pct,3}%";

            // Replace last line if same package, otherwise add new line
            if (progressLines.Count > 0 && progressLines[^1].Contains(name.EscapeMarkup()))
                progressLines[^1] = line;
            else
                progressLines.Add(line);

            // Take only the last N lines to simulate scrolling
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
            result = await operation(manager);
        });
        return result;
    }
}
