using PackageManager.Alpm;
using PackageManager.Aur;
using Shelly_CLI.Configuration;
using Shelly_CLI.Utility;
using Spectre.Console;

namespace Shelly_CLI.ConsoleLayouts;

/// <summary>
/// pacman/makepkg-style single-stream renderer for AUR install/upgrade flows.
/// Top-to-bottom log, in-place progress bars pinned to the bottom line(s),
/// section banners ("::", "==&gt;"), no Live panels.
/// </summary>
public static class AurSinglePaneOutput
{
    private sealed class BarState
    {
        public string Name = "";
        public ulong Current;
        public ulong HowMany;
        public int Pct;
        public string ActionType = "";
    }

    private readonly record struct LineKey(string Source, string Package, string Action);

    private sealed class StickySlot
    {
        public LineKey Key;
        public string Text = "";
        public DateTime LastUpdate;
    }

    public static async Task<bool> Output(
        AurPackageManager manager,
        Func<AurPackageManager, Task> operation,
        bool noConfirm = false)
    {
        var cfg = ConfigManager.ReadConfig();
        var style = ProgressBarRenderer.ParseStyle(cfg.ProgressBarStyle);
        var barWidth = cfg.ProgressBarWidth;
        var maxStickies = Math.Max(1, cfg.SinglePaneMaxStickies);

        // Single lock guards stdout writes and bar state.
        var ioLock = new object();
        var bars = new Dictionary<string, BarState>(StringComparer.Ordinal);
        var order = new List<string>();
        var barRowsDrawn = 0;

        // Multi-slot sticky in-place lines (between scrollback and bars).
        var stickies = new List<StickySlot>();
        var stickyDrawnCount = 0;

        var hadError = false;
        var pendingPacfiles = new List<PendingPacfile>();
        var pacfileLock = new object();

        // In-place repaint requires a real TTY with ANSI; both bar styles use it.
        var animate = !Console.IsOutputRedirected
                      && AnsiConsole.Profile.Capabilities.Ansi;
        var frame = 0;


        manager.PackageProgress += (_, args) =>
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

            var msg = args.Message != null ? $" - {args.Message.EscapeMarkup()}" : "";
            var line =
                $"[bold]::[/] [{statusColor}]({args.CurrentIndex}/{args.TotalCount}) " +
                $"{args.Status.ToString().ToLowerInvariant()} {args.PackageName.EscapeMarkup()}[/]{msg}";

            if (args.Status == PackageProgressStatus.Completed
                || args.Status == PackageProgressStatus.Failed)
            {
                // Finalize any in-place lines for this package, then emit final status to scrollback.
                lock (ioLock) { FinalizeStickiesWhere(s => s.Key.Source == "progress" && s.Key.Package == args.PackageName); }
                WriteLine(line);

                // Promote / remove bar (if any).
                lock (ioLock)
                {
                    if (bars.Remove(args.PackageName))
                    {
                        order.Remove(args.PackageName);
                        ClearBars();
                        DrawBars();
                    }
                }
            }
            else
            {
                // Intermediate phase update — coalesce same (package, status) into one in-place line.
                WriteEvent(
                    new LineKey("progress", args.PackageName, args.Status.ToString()),
                    line);

                // makepkg banner before build (one-shot, not coalesced).
                if (args.Status == PackageProgressStatus.Building)
                {
                    WriteLine($"[bold]==>[/] Making package: [bold]{args.PackageName.EscapeMarkup()}[/]");
                }
            }
        };

        manager.Progress += (_, e) =>
        {
            var name = e.PackageName ?? "unknown";
            var pct = e.Percent ?? 0;
            var actionType = e.ProgressType.ToString();

            lock (ioLock)
            {
                if (!bars.TryGetValue(name, out var r))
                {
                    r = new BarState { Name = name };
                    bars[name] = r;
                    order.Add(name);
                }

                r.Current = e.Current ?? 0;
                r.HowMany = e.HowMany ?? 0;
                r.Pct = pct;
                r.ActionType = actionType;

                if (!animate)
                {
                    // Plain mode: print only on completion.
                    if (pct >= 100)
                    {
                        Console.Out.WriteLine(RenderBarLine(r));
                        bars.Remove(name);
                        order.Remove(name);
                    }
                    return;
                }

                ClearBars();
                if (pct >= 100)
                {
                    // Promote completed bar to history.
                    Console.Out.WriteLine(RenderBarLine(r));
                    bars.Remove(name);
                    order.Remove(name);
                }
                DrawBars();
            }
        };

        manager.BuildOutput += (_, e) =>
        {
            if (e.Percent.HasValue)
            {
                // makepkg's own progress: coalesce repeated percent ticks into a single in-place line.
                var bar = ProgressBarRenderer.RenderStatic(e.Percent.Value, 20);
                var msgPart = (e.ProgressMessage ?? "").EscapeMarkup();
                var rendered =
                    $"[bold]{e.PackageName.EscapeMarkup()}[/] [yellow]{bar} {e.Percent.Value,3}%[/] {msgPart}";
                // Action key uses the progress message (e.g. "Downloading foo.tar.gz") so that
                // distinct files within the same package each get their own finalized history line.
                var action = string.IsNullOrEmpty(e.ProgressMessage) ? "build" : e.ProgressMessage!;
                WriteEvent(new LineKey("build", e.PackageName ?? "", action), rendered);

                // Finalize once we hit 100% so the completed line stays in scrollback.
                if (e.Percent.Value >= 100)
                {
                    var key = new LineKey("build", e.PackageName ?? "", action);
                    lock (ioLock) { FinalizeSticky(key); }
                }
                return;
            }

            // Non-progress build line for this package interrupts any running in-place build runs for it.
            var pkg = e.PackageName ?? "";
            lock (ioLock) { FinalizeStickiesWhere(s => s.Key.Source == "build" && s.Key.Package == pkg); }

            var line = e.Line ?? string.Empty;
            if (e.IsError)
            {
                WriteLine($"[red]{line.EscapeMarkup()}[/]");
            }
            else
            {
                // Forward verbatim. Tag warnings/errors only when prefix matches pacman convention.
                if (line.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                    WriteLine($"[red]{line.EscapeMarkup()}[/]");
                else if (line.StartsWith("warning:", StringComparison.OrdinalIgnoreCase))
                    WriteLine($"[yellow]{line.EscapeMarkup()}[/]");
                else if (line.StartsWith("==>"))
                    WriteLine($"[bold green]{line.EscapeMarkup()}[/]");
                else if (line.StartsWith("  ->"))
                    WriteLine($"[bold blue]{line.EscapeMarkup()}[/]");
                else
                    WritePlain(line);
            }
        };

        manager.PackageOperation += (_, e) =>
        {
            WriteLine(string.IsNullOrWhiteSpace(e.PackageName)
                ? $":: {e.EventType}".EscapeMarkup()
                : $":: {e.EventType} for {e.PackageName}".EscapeMarkup());
        };

        manager.ScriptletInfo += (_, e) =>
        {
            var line = e.Line ?? string.Empty;
            WriteLine(string.IsNullOrEmpty(line)
                ? "[dim]Running scriptlet...[/]"
                : $"[dim]Scriptlet: {line.EscapeMarkup()}[/]");
        };

        manager.HookRun += (_, e) =>
        {
            var line = e.Description ?? string.Empty;
            WriteLine(string.IsNullOrEmpty(line)
                ? "[dim]Running hook...[/]"
                : $"[dim]Hook: {line.EscapeMarkup()}[/]");
        };

        manager.Replaces += (_, e) =>
        {
            WriteLine($":: {e.Repository.EscapeMarkup()}/{e.PackageName.EscapeMarkup()} replaces " +
                      $"{string.Join(",", e.Replaces.Select(r => r.EscapeMarkup()))}");
        };

        manager.PacnewInfo += (_, e) =>
        {
            WriteLine($"[yellow]:: pacnew stored @ {e.FileLocation.EscapeMarkup()}.pacnew[/]");
            lock (pacfileLock)
            {
                pendingPacfiles.Add(new PendingPacfile(PacfileKind.Pacnew, null,
                    e.FileLocation + ".pacnew", DateTime.UtcNow));
            }
        };

        manager.PacsaveInfo += (_, e) =>
        {
            WriteLine($"[yellow]:: pacsave stored @ {e.FileLocation.EscapeMarkup()}.pacsave[/]");
            lock (pacfileLock)
            {
                pendingPacfiles.Add(new PendingPacfile(PacfileKind.Pacsave, e.OldPackage,
                    e.FileLocation + ".pacsave", DateTime.UtcNow));
            }
        };

        manager.ErrorEvent += (_, e) =>
        {
            hadError = true;
            WriteLine($"[red]error:[/] {e.Error.EscapeMarkup()}");
        };

        manager.Question += (_, e) =>
        {
            if (noConfirm)
            {
                QuestionHandler.HandleQuestion(e, uiMode: false, noConfirm: true);
                return;
            }

            // No Live region active — safe to prompt directly.
            lock (ioLock) { FinalizeAllStickies(); ClearBars(); }
            try
            {
                QuestionHandler.HandleQuestion(e, uiMode: false, noConfirm: false);
            }
            finally
            {
                lock (ioLock) { DrawBars(); }
            }
        };

        manager.PkgbuildDiffRequest += (_, args) =>
        {
            // Print the diff inline (above bars).
            lock (ioLock)
            {
                ClearBars();
                AnsiConsole.MarkupLine($"[bold]:: PKGBUILD for {args.PackageName.EscapeMarkup()}:[/]");
                foreach (var line in AurSplitOutput.BuildUnifiedDiffLines(
                             args.OldPkgbuild ?? string.Empty,
                             args.NewPkgbuild ?? string.Empty))
                {
                    AnsiConsole.MarkupLine(line);
                }
                DrawBars();
            }

            if (noConfirm)
            {
                args.ProceedWithUpdate = true;
                return;
            }

            // Safe direct prompt — no Live region.
            lock (ioLock) { ClearBars(); }
            try
            {
                args.ProceedWithUpdate =
                    AnsiConsole.Confirm(":: Proceed with this PKGBUILD?", true);
            }
            finally
            {
                lock (ioLock) { DrawBars(); }
            }
        };


        WriteLine("[bold]::[/] Synchronizing package databases...");

        using var frameCts = new CancellationTokenSource();
        Task? ticker = null;
        if (animate && ProgressBarRenderer.ShouldAnimate(style))
        {
            ticker = Task.Run(async () =>
            {
                try
                {
                    while (!frameCts.IsCancellationRequested)
                    {
                        await Task.Delay(120, frameCts.Token);
                        lock (ioLock)
                        {
                            frame++;
                            if (bars.Count > 0)
                            {
                                ClearBars();
                                DrawBars();
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, frameCts.Token);
        }

        try
        {
            await operation(manager);
        }
        catch (Exception ex)
        {
            hadError = true;
            WriteLine($"[red]error:[/] {ex.Message.EscapeMarkup()}");
        }
        finally
        {
            frameCts.Cancel();
            if (ticker != null)
            {
                try { await ticker; } catch { }
            }
            // Ensure cursor lands below any active bars so the shell prompt
            // doesn't overwrite a partially-drawn bar.
            lock (ioLock)
            {
                FinalizeAllStickies();
                ClearBars();
                bars.Clear();
                order.Clear();
            }
        }

        WriteLine(hadError
            ? "[red]:: Transaction failed.[/]"
            : "[green]:: Transaction complete.[/]");

        try
        {
            await PacfileFlusher.FlushAsync(pendingPacfiles, pacfileLock);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]warning:[/] failed to store pacfiles: {ex.Message.EscapeMarkup()}");
        }

        return !hadError;

        void WriteLine(string markup)
        {
            lock (ioLock)
            {
                ClearBars();
                AnsiConsole.MarkupLine(markup);
                DrawBars();
            }
        }

        void WritePlain(string text)
        {
            lock (ioLock)
            {
                ClearBars();
                Console.Out.WriteLine(text);
                DrawBars();
            }
        }

        void DrawBars()
        {
            if (!animate) return;
            DrawStickies();
            foreach (var key in order)
            {
                Console.Out.Write(RenderBarLine(bars[key]));
                Console.Out.Write("\n");
                barRowsDrawn++;
            }
            Console.Out.Flush();
        }

        static string TruncateStickyText(string text)
        {
            var max = Math.Max(20, Console.WindowWidth - 1);
            if (Markup.Remove(text).Length <= max) return text;
            // Fall back to plain truncation when too long.
            var plain = Markup.Remove(text);
            if (plain.Length > max) plain = plain[..max];
            return plain.EscapeMarkup();
        }

        void DrawStickies()
        {
            if (!animate || stickies.Count == 0 || stickyDrawnCount > 0) return;
            foreach (var s in stickies)
            {
                AnsiConsole.MarkupLine(TruncateStickyText(s.Text));
            }
            stickyDrawnCount = stickies.Count;
        }

        void ClearStickies()
        {
            if (!animate || stickyDrawnCount == 0) return;
            for (var i = 0; i < stickyDrawnCount; i++)
            {
                Console.Out.Write("\x1b[1A\x1b[2K");
            }
            Console.Out.Write("\r");
            stickyDrawnCount = 0;
        }

        // Finalize a specific sticky into scrollback.
        void FinalizeSticky(LineKey key)
        {
            var idx = stickies.FindIndex(s => s.Key.Equals(key));
            if (idx < 0) return;
            var slot = stickies[idx];
            stickies.RemoveAt(idx);
            if (animate)
            {
                ClearBars(); // clears bars + stickies
                AnsiConsole.MarkupLine(slot.Text);
                DrawBars();
            }
            else
            {
                AnsiConsole.MarkupLine(slot.Text);
            }
        }

        void FinalizeStickiesWhere(Func<StickySlot, bool> predicate)
        {
            var matched = stickies.Where(predicate).ToList();
            if (matched.Count == 0) return;
            if (animate) ClearBars();
            foreach (var s in matched)
            {
                stickies.Remove(s);
                AnsiConsole.MarkupLine(s.Text);
            }
            if (animate) DrawBars();
        }

        void FinalizeAllStickies()
        {
            if (stickies.Count == 0) return;
            if (animate) ClearBars();
            foreach (var s in stickies)
            {
                AnsiConsole.MarkupLine(s.Text);
            }
            stickies.Clear();
            if (animate) DrawBars();
        }

        void EnsureCapacityForNewSticky()
        {
            while (stickies.Count >= maxStickies)
            {
                // Evict least-recently-updated slot (oldest by LastUpdate, falling back to insertion order).
                var victimIdx = 0;
                var victimTime = stickies[0].LastUpdate;
                for (var i = 1; i < stickies.Count; i++)
                {
                    if (stickies[i].LastUpdate < victimTime)
                    {
                        victimTime = stickies[i].LastUpdate;
                        victimIdx = i;
                    }
                }
                var victim = stickies[victimIdx];
                stickies.RemoveAt(victimIdx);
                AnsiConsole.MarkupLine(victim.Text);
            }
        }

        void WriteEvent(LineKey key, string markup)
        {
            lock (ioLock)
            {
                var slot = stickies.FirstOrDefault(s => s.Key.Equals(key));
                if (slot is null)
                {
                    if (animate) ClearBars();
                    EnsureCapacityForNewSticky();
                    slot = new StickySlot { Key = key, Text = markup, LastUpdate = DateTime.UtcNow };
                    stickies.Add(slot);
                    if (animate) DrawBars();
                    // !animate: suppress intermediate prints; emitted only on finalize.
                    return;
                }

                slot.Text = markup;
                slot.LastUpdate = DateTime.UtcNow;
                if (animate)
                {
                    ClearBars();
                    DrawBars();
                }
            }
        }

        string RenderBarLine(BarState r)
        {
            var bar = ProgressBarRenderer.Render(r.Pct, frame, style, barWidth);
            var name = r.Name;
            var line = $"({r.Current}/{r.HowMany}) {r.ActionType} {name} {bar} {r.Pct,3}%";
            // Keep a 1-char margin to avoid wrap-induced double rows.
            // Use printable (markup-stripped) length since Pacman bar contains Spectre markup.
            var max = Math.Max(20, Console.WindowWidth - 1);
            if (Markup.Remove(line).Length > max)
            {
                line = Markup.Remove(line);
                if (line.Length > max) line = line[..max];
            }
            return line;
        }

        void ClearBars()
        {
            if (!animate)
            {
                return;
            }
            if (barRowsDrawn > 0)
            {
                // Move up barRowsDrawn lines and erase each.
                for (var i = 0; i < barRowsDrawn; i++)
                {
                    Console.Out.Write("\x1b[1A\x1b[2K");
                }
                Console.Out.Write("\r");
                barRowsDrawn = 0;
            }
            ClearStickies();
        }
    }
}
