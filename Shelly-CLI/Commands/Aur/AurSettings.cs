using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Aur;

public sealed class AurSearchSettings : DefaultSettings
{
    [CommandArgument(0, "<query>")]
    [Description("Search term for AUR packages")]
    public string Query { get; init; } = string.Empty;
}

public class AurPackageSettings : CommandSettings
{
    [CommandArgument(0, "<packages>")]
    [Description("Package name(s) to operate on (space-separated)")]
    public string[] Packages { get; set; } = [];

    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompts")]
    public bool NoConfirm { get; set; }
}

public class AurInstallVersionSettings : CommandSettings
{
    [CommandArgument(0, "<package>")]
    [Description("Package name to install")]
    public string Package { get; set; } = string.Empty;

    [CommandArgument(1, "<commit>")]
    [Description("Git commit hash for the specific version")]
    public string Commit { get; set; } = string.Empty;
}

public class AurUpgradeSettings : CommandSettings
{
    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompts")]
    public bool NoConfirm { get; set; }
}
