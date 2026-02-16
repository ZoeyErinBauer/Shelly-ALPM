using System.IO;
using System.Text;

namespace Shelly_CLI;

public class StderrPrefixWriter : TextWriter
{
    private readonly TextWriter _stderr;
    private const string ShellyPrefix = "[Shelly]";

    public StderrPrefixWriter(TextWriter stderr)
    {
        _stderr = stderr;
    }

    public override void WriteLine(string? value)
    {
        _stderr.WriteLine($"{ShellyPrefix}{value}");
    }

    public override void Write(string? value)
    {
        _stderr.Write(value);
    }

    public override void Write(char value)
    {
        _stderr.Write(value);
    }

    public override void Flush()
    {
        _stderr.Flush();
    }

    public override Encoding Encoding => _stderr.Encoding;
}
