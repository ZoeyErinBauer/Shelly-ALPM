using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public sealed class AurSearchSettings : DefaultSettings
{
    [CommandArgument(0, "<query>")]
    [Description("Search term to find packages in the Arch User Repository")]
    public string Query { get; init; } = string.Empty;
}

public class AurPackageSettings : CommandSettings
{
    [CommandArgument(0, "<packages>")]
    [Description("One or more AUR package names to operate on (space-separated)")]
    public string[] Packages { get; set; } = [];

    [CommandOption("--no-confirm")]
    [Description("Proceed without asking for user confirmation")]
    public bool NoConfirm { get; set; }
}

public class AurInstallVersionSettings : CommandSettings
{
    [CommandArgument(0, "<package>")]
    [Description("Name of the AUR package to install")]
    public string Package { get; set; } = string.Empty;

    [CommandArgument(1, "<commit>")]
    [Description("Git commit hash specifying the exact version to install")]
    public string Commit { get; set; } = string.Empty;
}

public class AurUpgradeSettings : CommandSettings
{
    [CommandOption("--no-confirm")]
    [Description("Proceed without asking for user confirmation")]
    public bool NoConfirm { get; set; }
}
