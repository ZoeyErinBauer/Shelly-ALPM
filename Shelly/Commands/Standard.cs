using System.Diagnostics.CodeAnalysis;
using ConsoleAppFramework;
using PackageManager.Alpm;

// ReSharper disable InvalidXmlDocComment

namespace Shelly.Commands;

[RegisterCommands]
[SuppressMessage("GenerateConsoleAppFramework", "CAF007:Command name is duplicated.")]
internal class Standard
{
    /// <summary>
    /// Synchronize package databases
    /// </summary>
    /// <param name="force">-f,force the sync of the package databases </param>
    /// <returns></returns>
    public int Sync(ConsoleAppContext context, bool force = false)
    {
        var globals = (GlobalOptions)context.GlobalOptions!;
        return globals.UiMode ? SyncUiMode(force) : SyncConsoleMode(force);
    }

    private int SyncUiMode(bool force = false)
    {
        var manager = new AlpmManager(Configuration.GetConfigurationFile());
        Console.WriteLine("Synchronizing package databases...");
        manager.Progress += (sender, args) => { Console.WriteLine($"{args.PackageName}: {args.Percent}%"); };
        manager.Sync(force);
        Console.WriteLine("Package databases synchronization completed");
        return 0;
    }

    private int SyncConsoleMode(bool force = false)
    {
        var manager = new AlpmManager(Configuration.GetConfigurationFile());
        Console.WriteLine("Synchronizing package databases...");
        if (force)
        {
            Console.WriteLine("Forcing Synchronization");
        }

        var rowIndex = new Dictionary<string, int>();
        var rowCount = 0;
        object renderLock = new();

        manager.Progress += (sender, args) =>
        {
            lock (renderLock)
            {
                var name = args.PackageName ?? "unknown";
                var pct = args.Percent ?? 0;
                var bar = new string('\u2588', pct / 5) + new string('\u2591', 20 - pct / 5);
                var stage = args.ProgressType;

                var line = $"  {name,-30} {bar} {pct,3}%  {stage}";

                if (!rowIndex.TryGetValue(name, out var idx))
                {
                    rowIndex[name] = rowCount;
                    rowCount++;
                    Console.WriteLine(line);
                }
                else
                {
                    // Move cursor up to the correct row and overwrite
                    var linesUp = rowCount - idx;
                    Console.Write($"\x1b[{linesUp}A\r");
                    Console.Write(line.PadRight(Console.WindowWidth - 1));
                    Console.Write($"\x1b[{linesUp}B\r");
                }
            }
        };

        manager.Sync(force);
        Console.WriteLine();
        Console.WriteLine("Package databases synchronization completed");
        return 0;
    }
}