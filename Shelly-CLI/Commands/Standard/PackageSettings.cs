using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class PackageSettings : DefaultSettings
{
    [CommandArgument(0, "<packages>")]
    [Description("One or more package names to operate on (space-separated)")]
    public string[] Packages { get; set; } = [];

    [CommandOption("-n|--no-confirm")]
    [Description("Proceed without asking for user confirmation")]
    public bool NoConfirm { get; set; }
}
