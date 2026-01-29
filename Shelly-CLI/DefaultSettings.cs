using Spectre.Console.Cli;

namespace Shelly_CLI;

public class DefaultSettings : CommandSettings
{
    [CommandOption("--json")]
    public bool JsonOutput { get; set; }
    
    [CommandOption("-f|--force")]
    public bool Force { get; set; }
}