using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class SyncSettings : CommandSettings
{
    [CommandOption("-f|--force")]
    [Description("Force synchronization even if databases are up to date")]
    public bool Force { get; set; }
}
