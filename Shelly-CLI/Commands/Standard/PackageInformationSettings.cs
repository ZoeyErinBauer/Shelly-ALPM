using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class PackageInformationSettings : PackageSettings
{
    [CommandOption("-i | --installed")]
    [Description("Searches installed packages")]
    public bool SearchInstalled { get; set; }

    [CommandOption("-r | --repository")]
    [Description("Searches repository of available packages.")]
    public bool SearchRepository { get; set; }
}