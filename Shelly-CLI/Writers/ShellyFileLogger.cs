using System.Text;

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

    private const string LogPath = "/var/log/shelly.log";
    private const string RotatedLogPath = "/var/log/shelly.log.1";
    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5MB

    public ShellyFileLogger(TextWriter inner, StreamWriter fileWriter, string streamLabel)
    {
        _inner = inner;
        _fileWriter = fileWriter;
        _streamLabel = streamLabel;
    }

    public override void WriteLine(string? value)
    {
        _inner.WriteLine(value);
        FlushBufferToLog();
        _fileWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{_streamLabel}] {value}");
        _fileWriter.Flush();
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
        var line = _lineBuffer.ToString().TrimEnd('\r', '\n');
        if (line.Length > 0)
        {
            _fileWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{_streamLabel}] {line}");
            _fileWriter.Flush();
        }
        _lineBuffer.Clear();
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
