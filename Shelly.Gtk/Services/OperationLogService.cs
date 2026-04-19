using System.Text;
using System.Text.RegularExpressions;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;

public partial class OperationLogService : IOperationLogService
{
    private const string LogPath = "/var/log/shelly.log";
    private const string RotatedLogPath = "/var/log/shelly.log.1";

    [GeneratedRegex(@"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] SESSION START")]
    private static partial Regex SessionStartRegex();

    [GeneratedRegex(@"Command: (.+)$")]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"User: (.+) \(sudo: (yes|no)\)")]
    private static partial Regex UserRegex();

    [GeneratedRegex(@"SESSION END — exit code: (\d+)")]
    private static partial Regex SessionEndRegex();

    public async Task<List<string>> GetSessionExcerptAsync(OperationLogEntry entry, int maxBytes)
    {
        if (!File.Exists(entry.SourceFile))
            return [];

        var result = new List<string>();
        int totalBytes = 0;

        try
        {
            using var stream = File.OpenRead(entry.SourceFile);
            using var reader = new StreamReader(stream);

            int currentLine = 0;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (currentLine >= entry.StartLine && currentLine <= entry.EndLine)
                {
                    if (line != null)
                    {
                        int lineBytes = Encoding.UTF8.GetByteCount(line) + 1; 

                        if (totalBytes + lineBytes > maxBytes)
                        {
                                return [];
                        }

                        result.Add(line);
                        totalBytes += lineBytes;
                    }
                }

                if (currentLine > entry.EndLine)
                    break;

                currentLine++;
            }
        }
        catch
        {
            return [];
        }

        return result;
    }
    public async Task<List<OperationLogEntry>> GetRecentOperationsAsync(int count = 10)
    {
        var entries = new List<OperationLogEntry>();

        entries.AddRange(await ParseLogFileAsync(LogPath));

        if (entries.Count < count && File.Exists(RotatedLogPath))
        {
            entries.AddRange(await ParseLogFileAsync(RotatedLogPath));
        }

        return entries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    private static async Task<List<OperationLogEntry>> ParseLogFileAsync(string path)
    {
        var entries = new List<OperationLogEntry>();

        if (!File.Exists(path))
            return entries;

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(path);
        }
        catch
        {
            return entries;
        }

        OperationLogEntry? current = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var startMatch = SessionStartRegex().Match(line);
            if (startMatch.Success)
            {
                current = new OperationLogEntry
                {
                    SourceFile = path,
                    StartLine = i
                };

                if (DateTime.TryParse(startMatch.Groups[1].Value, out var ts))
                    current.Timestamp = ts;

                continue;
            }

            if (current == null) continue;

            var cmdMatch = CommandRegex().Match(line);
            if (cmdMatch.Success)
            {
                current.Command = cmdMatch.Groups[1].Value.Trim();
                continue;
            }

            var userMatch = UserRegex().Match(line);
            if (userMatch.Success)
            {
                current.User = userMatch.Groups[1].Value.Trim();
                current.IsSudo = userMatch.Groups[2].Value == "yes";
                continue;
            }

            var endMatch = SessionEndRegex().Match(line);
            if (endMatch.Success)
            {
                if (int.TryParse(endMatch.Groups[1].Value, out var exitCode))
                    current.ExitCode = exitCode;

                current.EndLine = i;
                entries.Add(current);
                current = null;
            }
        }

        if (current != null)
        {
            current.EndLine = lines.Length - 1;
            entries.Add(current);
        }

        return entries;
    }}
