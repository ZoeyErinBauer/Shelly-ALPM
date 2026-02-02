using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class UpgradeSettings : DefaultSettings
{
    [CommandOption("-n|--no-confirm")]
    [Description("Proceed with system upgrade without asking for user confirmation")]
    public bool NoConfirm { get; set; }
}
