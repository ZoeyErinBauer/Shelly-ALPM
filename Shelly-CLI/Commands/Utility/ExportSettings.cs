using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Utility;

public class ExportSettings : CommandSettings
{
    [CommandOption("-o|--output")]
    [CommandArgument(0, "<package>")]
    [Description("Output location for the exported sync (defaut: .cache/Shelly/sync.json)")]
    public string Output { get; set; } = string.Empty;
}