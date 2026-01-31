using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class PackageSettings : DefaultSettings
{
    [CommandArgument(0, "<packages>")]
    [Description("Package name(s) to operate on")]
    public string[] Packages { get; set; } = [];

    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompt")]
    public bool NoConfirm { get; set; }
}
