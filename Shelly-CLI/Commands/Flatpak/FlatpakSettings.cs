using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public sealed class FlathubSearchSettings : CommandSettings
{
    [CommandArgument(0, "<query>")]
    [Description("Search term to find Flatpak applications on Flathub")]
    public string Query { get; init; } = string.Empty;

    [CommandOption("-l|--limit <N>")]
    [Description("Maximum number of search results to display per page")]
    [DefaultValue(21)]
    public int Limit { get; init; } = 21;

    [CommandOption("-p|--page <N>")]
    [Description("Page number for paginated results (starts at 1)")]
    [DefaultValue(1)]
    public int Page { get; init; } = 1;

    [CommandOption("--no-ui")]
    [Description("Output raw JSON data instead of formatted display")]
    [DefaultValue(false)]
    public bool NoUi { get; init; } = false;
}

public class FlatpakPackageSettings : CommandSettings
{
    [CommandArgument(0, "<package>")]
    [Description("Flatpak application ID (e.g., com.spotify.Client)")]
    public string Packages { get; set; } = string.Empty;
}
