using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI;

public class ForceSettings : DefaultSettings
{
    [CommandOption("-f|--force")]
    [Description("Force the operation even if it would normally be skipped or blocked")]
    public bool Force { get; set; }
}
