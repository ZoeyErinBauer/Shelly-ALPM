using System.Text;
using System.Text.RegularExpressions;

namespace Shelly.Writers;

/// <summary>
/// A TextWriter that tees output to a log file with timestamps.
/// Buffers partial writes and flushes to the log on newline or carriage return.
/// Includes simple size-based log rotation.
/// </summary>
public class ShellyFileLogger : TextWriter
{
    private readonly TextWriter _inner;
    private readonly StreamWriter _fileWriter;
    private readonly string _streamLabel;
    private readonly StringBuilder _lineBuffer = new();
    private string? _lastLoggedLine;
    
    private readonly List<string> _tuiFrameBuffer = [];
    private bool _inTuiFrame;
    
    private static readonly Regex AnsiEscape = new(@"\x1B\[[\?0-9;]*[a-zA-Z]", RegexOptions.Compiled);
    private static readonly Regex[] NoisePatterns =
    [
        new(@"ALPM Progress:.*%:\s*\d+", RegexOptions.Compiled),
        new(@"Reading content stream", RegexOptions.Compiled),
    ];
    private static readonly Regex BoxBorder = new(@"^[\s┌┐└┘─│╔╗╚╝═║]+$", RegexOptions.Compiled);
    private static readonly Regex BoxContent = new(@"│([^│]*)│?", RegexOptions.Compiled);
    
    private const string LogPath = "/var/log/shelly.log";
    private const string RotatedLogPath = "/var/log/shelly.log.1";
    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5MB

    public ShellyFileLogger(TextWriter inner, StreamWriter fileWriter, string streamLabel)
    {
        _inner = inner;
        _fileWriter = fileWriter;
        _streamLabel = streamLabel;
    }

    private void FlushTuiFrame()
    {
        bool isHeader = true;
        var cells = new List<string>();

        foreach (var line in _tuiFrameBuffer)
        {
            if (isHeader) { isHeader = false; continue; }
            var lineCells = BoxContent.Matches(line)
                .Select(m => m.Groups[1].Value.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c)
                            && !c.All(ch => ch is '█' or '░' or ' ')
                            && !BoxBorder.IsMatch(c));

            cells.AddRange(lineCells);
        }
        if (cells.Count > 0)
            WriteToLog(string.Join(" | ", cells));
    }
    
    private void HandleLogLine(string raw)
    {
        var clean = AnsiEscape.Replace(raw, "").Trim();
        if (string.IsNullOrEmpty(clean)) return;

        if (clean.StartsWith('┌'))
        {
            _inTuiFrame = true;
            _tuiFrameBuffer.Clear();
            return;
        }

        if (_inTuiFrame)
        {
            if (clean.StartsWith('└'))
            {
                _inTuiFrame = false;
                FlushTuiFrame();
                return;
            }
            if (clean.StartsWith('├')) return;

            _tuiFrameBuffer.Add(clean);
            return;
        }
        if (NoisePatterns.Any(p => p.IsMatch(clean))) return;
        WriteToLog(clean);
    }
    
    private void WriteToLog(string clean)
    {
        if (clean == _lastLoggedLine) return;
        _lastLoggedLine = clean;
        _fileWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{_streamLabel}] {clean}");
        _fileWriter.Flush();    
    }
    
    public override void WriteLine(string? value)
    {
        _inner.WriteLine(value);
        FlushBufferToLog();
        if (value != null) HandleLogLine(value);  
    }
    
    public override void Write(string? value)
    {
        _inner.Write(value);
        if (string.IsNullOrEmpty(value)) return;

        _lineBuffer.Append(value);
        if (value.Contains('\n') || value.Contains('\r'))
        {
            FlushBufferToLog();
        }
    }

    public override void Write(char value)
    {
        _inner.Write(value);
        _lineBuffer.Append(value);
        if (value is '\n' or '\r')
        {
            FlushBufferToLog();
        }
    }

    public override void Flush()
    {
        _inner.Flush();
        _fileWriter.Flush();
    }

    public override Encoding Encoding => _inner.Encoding;

    private void FlushBufferToLog()
    {
        if (_lineBuffer.Length == 0) return;
        var raw = _lineBuffer.ToString().TrimEnd('\r', '\n');
        _lineBuffer.Clear();
        if (raw.Length > 0) HandleLogLine(raw);    
    }
    
    public static StreamWriter? OpenLogFile()
    {
        try
        {
            RotateIfNeeded();
            return new StreamWriter(LogPath, append: true) { AutoFlush = false };
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void WriteSessionHeader(StreamWriter writer, string[] args)
    {
        var user = Environment.GetEnvironmentVariable("SUDO_USER")
                   ?? Environment.GetEnvironmentVariable("USER")
                   ?? "unknown";
        var isSudo = Environment.GetEnvironmentVariable("SUDO_USER") != null;

        writer.WriteLine("=====================================");
        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SESSION START");
        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Command: shelly {string.Join(' ', args)}");
        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] User: {user} (sudo: {(isSudo ? "yes" : "no")})");
        writer.WriteLine("=====================================");
        writer.Flush();
    }

    /// <summary>
    /// Writes a session footer to the log file.
    /// </summary>
    public static void WriteSessionFooter(StreamWriter writer, int exitCode)
    {
        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SESSION END — exit code: {exitCode}");
        writer.Flush();
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogPath)) return;

            var info = new FileInfo(LogPath);
            if (info.Length < MaxLogSizeBytes) return;

            // Simple rotation: current -> .1 (overwrite previous .1)
            if (File.Exists(RotatedLogPath))
                File.Delete(RotatedLogPath);

            File.Move(LogPath, RotatedLogPath);
        }
        catch
        {
            // Best effort — if rotation fails, continue logging to the existing file
        }
    }
}
