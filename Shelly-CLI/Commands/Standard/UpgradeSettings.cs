using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class UpgradeSettings : DefaultSettings
{
    [CommandOption("-n | --no-confirm")]
    [Description("Proceed with system upgrade without asking for user confirmation")]
    public bool NoConfirm { get; set; }

    [CommandOption("-a | --all")]
    [Description("Upgrades all supported sources. (Standard, AUR, Flatpak")]
    public bool All { get; set; }

    [CommandOption("-u | --aur")]
    [Description("Upgrades AUR packages")]
    public bool Aur { get; set; }

    [CommandOption("-l | --flatpak")]
    [Description("Upgrade Flatpak packages")]
    public bool Flatpak { get; set; }
}