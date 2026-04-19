using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class ArchNewsSettings : CommandSettings
{
    [CommandOption("-a|--all")]
    [Description("Shows all arch news")]
    [DefaultValue(false)]
    public bool All { get; set; } = false;
    
    [CommandOption("-j|--json")]
    [Description("Returns news in JSON format")]
    [DefaultValue(false)]
    public bool Json { get; set; } = false;
}