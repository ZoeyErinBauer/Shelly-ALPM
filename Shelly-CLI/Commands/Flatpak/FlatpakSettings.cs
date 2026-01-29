using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public sealed class FlathubSearchSettings : CommandSettings
{
    [CommandArgument(0, "<query>")]
    [Description("Search term (passed to Flathub API as 'q')")]
    public string Query { get; init; } = string.Empty;

    [CommandOption("--limit <N>")]
    [Description("Max number of results to show")]
    [DefaultValue(21)]
    public int Limit { get; init; } = 21;

    [CommandOption("--page <N>")]
    [Description("Page number (1-based)")]
    [DefaultValue(1)]
    public int Page { get; init; } = 1;
    
    [CommandOption("--flatpak-ui-mode")]
    [Description("Returns Raw json")]
    [DefaultValue(false)]
    public bool UiMode { get; init; } = false;
}

public class FlatpakPackageSettings : CommandSettings
{
    [CommandArgument(0, "<package>")]
    [Description("Package name to operate on")]
    public string Packages { get; set; } = string.Empty;
}

public class FlatpakListSettings : CommandSettings
{
    [CommandOption("--flatpak-ui-mode")]
    [Description("Returns Raw json")]
    public bool UiMode { get; set; }
}
