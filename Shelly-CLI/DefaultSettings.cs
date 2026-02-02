using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI;

public class DefaultSettings : CommandSettings
{
    [CommandOption("-j|--json")]
    [Description("Output results in JSON format for UI integration and scripting")]
    public bool JsonOutput { get; set; }

    [CommandOption("-s|--sync")]
    [Description("Synchronize package databases before performing the operation")]
    public bool Sync { get; set; }
}