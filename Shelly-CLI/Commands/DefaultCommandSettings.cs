using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class DefaultCommandSettings : CommandSettings
{
    [CommandArgument(0, "[SearchString]")]
    [Description("Search")]
    public string SearchString { get; set; } = string.Empty;
    
    [CommandOption("-v | --version")]
    [Description("Show version information")]
    public bool Version { get; set; }

}