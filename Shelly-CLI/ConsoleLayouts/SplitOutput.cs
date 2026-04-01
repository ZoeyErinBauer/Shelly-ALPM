using PackageManager.Alpm;
using Shelly_CLI.Utility;
using Spectre.Console;

namespace Shelly_CLI.ConsoleLayouts;

public static class SplitOutput
{
    public static async Task Output(IAlpmManager manager, Func<IAlpmManager, Task> operation, bool noConfirm = false,
        int consoleRation = 3,
        int progressRatio = 2)
    {
        var currentPkgIndex = 0;
        string? lastPackageName = null;
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

            if (name != lastPackageName)
            {
                if (lastPackageName != null)
                    currentPkgIndex++;
                lastPackageName = name;
            }

            var line =
                $"({currentPkgIndex + 1}/{e.HowMany}) {actionType} [bold]{name.EscapeMarkup()}[/] [green]{bar}[/] {pct,3}%";

            // Replace last line if same package, otherwise add new line
            if (progressLines.Count > 0 && progressLines[^1].Contains(name.EscapeMarkup()))
                progressLines[^1] = line;
            else
                progressLines.Add(line);

            // Take only the last N lines to simulate scrolling
            lock (renderLock)
            {
                var visible = progressLines.Skip(Math.Max(0, progressLines.Count - maxVisibleLines)).ToList();
                layout["Progress"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Progress")
                        .Expand());
                liveCtx?.Refresh();
            }
        };

        manager.PackageOperation += (sender, e) =>
        {
            lock (renderLock)
            {
                consoleLines.Add(string.IsNullOrWhiteSpace(e.PackageName)
                    ? $"{e.EventType}".EscapeMarkup()
                    : $"{e.EventType} for {e.PackageName}".EscapeMarkup());

                var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                layout["Console"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Console")
                        .Expand());
                liveCtx?.Refresh();
            }
        };

        manager.ScriptletInfo += (sender, e) =>
        {
            lock (renderLock)
            {
                var line = e.Line ?? string.Empty;
                consoleLines.Add(string.IsNullOrEmpty(line)
                    ? "Running scriptlet..."
                    : $"Scriptlet: {line.EscapeMarkup()}");
                var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                layout["Console"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Console")
                        .Expand());
                liveCtx?.Refresh();
            }
        };

        manager.HookRun += (sender, e) =>
        {
            lock (renderLock)
            {
                var line = e.Description ?? string.Empty;
                consoleLines.Add(string.IsNullOrEmpty(line)
                    ? "Running hook..."
                    : $"Hook: {line.EscapeMarkup()}");
                var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                layout["Console"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Console")
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

            if (e.QuestionType == AlpmQuestionType.SelectProvider)
            {
                HandleProviderInConsole(e, consoleLines, maxVisibleLines, layout, liveCtx, renderLock);
                return;
            }

            // Show yes/no question in console section
            lock (renderLock)
            {
                consoleLines.Add($"[yellow bold]{e.QuestionText.EscapeMarkup()}[/]");
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
                        consoleLines.Add("[green]> Yes[/]");
                        var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        liveCtx?.Refresh();
                    }

                    e.SetResponse(1);
                    break;
                }

                if (key.Key == ConsoleKey.N)
                {
                    lock (renderLock)
                    {
                        consoleLines.Add("[red]> No[/]");
                        var visible = consoleLines.Skip(Math.Max(0, consoleLines.Count - maxVisibleLines)).ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        liveCtx?.Refresh();
                    }

                    e.SetResponse(0);
                    break;
                }
            }
        };

        await AnsiConsole.Live(layout).StartAsync(async ctx =>
        {
            liveCtx = ctx;
            await operation(manager);
        });
    }

    private static void HandleProviderInConsole(AlpmQuestionEventArgs question,
        List<string> consoleLines, int maxVisibleLines, Layout layout, LiveDisplayContext? ctx, object renderLock)
    {
        if (question.ProviderOptions is null) return;

        int selectedIndex = 0;
        consoleLines.Add($"[yellow bold]{question.QuestionText.EscapeMarkup()}[/]");
        int optionStartIndex = consoleLines.Count;

        void RenderSelection()
        {
            // Remove old option lines
            if (consoleLines.Count > optionStartIndex)
                consoleLines.RemoveRange(optionStartIndex, consoleLines.Count - optionStartIndex);

            for (int i = 0; i < question.ProviderOptions.Count; i++)
            {
                var prefix = i == selectedIndex ? "[green]> [/]" : "  ";
                var style = i == selectedIndex ? "[bold green]" : "[dim]";
                consoleLines.Add($"{prefix}{style}{question.ProviderOptions[i].EscapeMarkup()}[/]");
            }

            lock (renderLock)
            {
                var visible = consoleLines
                    .Skip(Math.Max(0, consoleLines.Count - maxVisibleLines))
                    .ToList();
                layout["Console"].Update(
                    new Panel(new Markup(string.Join("\n", visible)))
                        .Header("Console").Expand());
                ctx?.Refresh();
            }
        }

        RenderSelection();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = Math.Max(0, selectedIndex - 1);
                    RenderSelection();
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = Math.Min(question.ProviderOptions.Count - 1, selectedIndex + 1);
                    RenderSelection();
                    break;
                case ConsoleKey.Enter:
                    // Replace options with final selection
                    consoleLines.RemoveRange(optionStartIndex, consoleLines.Count - optionStartIndex);
                    consoleLines.Add($"[green]Selected: {question.ProviderOptions[selectedIndex].EscapeMarkup()}[/]");
                    question.SetResponse(selectedIndex);

                    lock (renderLock)
                    {
                        var visible = consoleLines
                            .Skip(Math.Max(0, consoleLines.Count - maxVisibleLines))
                            .ToList();
                        layout["Console"].Update(
                            new Panel(new Markup(string.Join("\n", visible)))
                                .Header("Console").Expand());
                        ctx?.Refresh();
                    }

                    return;
            }
        }
    }
}