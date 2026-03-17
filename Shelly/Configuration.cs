using Shelly.Configurations;

namespace Shelly;

public static class Configuration
{
    public static string GetConfigurationFilePath() => "/etc/pacman.conf";

    public static string GetSizeDisplay() => ConfigManager.ReadConfig().FileSizeDisplay;
}