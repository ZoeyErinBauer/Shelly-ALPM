using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class CorruptedPackagesSettings : CommandSettings
{
    [CommandOption("-n|--no-confirm")]
    public bool NoConfirm { get; set; }
    
    [CommandOption("-d|--dry-run")]
    public bool DryRun { get; set; }
    
}