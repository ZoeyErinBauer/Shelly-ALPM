using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class UpgradeSettings : DefaultSettings
{
    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompt")]
    public bool NoConfirm { get; set; }
}
