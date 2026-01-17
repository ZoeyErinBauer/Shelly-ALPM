using Shelly.Utilities.Extensions;
using Shelly.Utilities.System.Enums;

namespace Shelly.Utilities.System;

public static class EnvironmentManager
{
    private const string DesktopEnvironmentVariable = "XDG_CURRENT_DESKTOP";

    public static string CreateWindowManagerVars()
    {
        return GetDesktopEnvironment() switch
        {
            SupportedDesktopEnvironments.KDE or SupportedDesktopEnvironments.GNOME or SupportedDesktopEnvironments.XFCE
                or SupportedDesktopEnvironments.Cinnamon or SupportedDesktopEnvironments.MATE
                or SupportedDesktopEnvironments.LXQt or SupportedDesktopEnvironments.LXDE
                or SupportedDesktopEnvironments.Budgie or SupportedDesktopEnvironments.Pantheon
                or SupportedDesktopEnvironments.COSMIC => "",
            SupportedDesktopEnvironments.Hyprland or SupportedDesktopEnvironments.Sway
                or SupportedDesktopEnvironments.Niri or SupportedDesktopEnvironments.i3
                or SupportedDesktopEnvironments.Unknown => CreateWMLaunchVars(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string CreateWMLaunchVars()
    {
        List<string> convertedVars = [];
        var envVars = EnumExtensions.ToNameList<WindowManagerEnvVariables>();
        convertedVars.AddRange(from envVar in envVars
            let value = Environment.GetEnvironmentVariable(envVar)
            where !string.IsNullOrEmpty(value)
            select $"{envVar}={value}");

        return convertedVars.Count > 0 ? $" {string.Join(" ", convertedVars)} " : "";
    }

    public static SupportedDesktopEnvironments GetDesktopEnvironment() =>
        Enum.TryParse<SupportedDesktopEnvironments>(Environment.GetEnvironmentVariable(DesktopEnvironmentVariable),
            true, out var result)
            ? result
            : SupportedDesktopEnvironments
                .Unknown;
}