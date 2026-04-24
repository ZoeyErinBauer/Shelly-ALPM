using System;
using System.IO;

namespace PackageManager.Utilities;

public static class XdgPaths
{
    public static string ConfigHome() => Resolve("XDG_CONFIG_HOME", ".config");
    public static string CacheHome() => Resolve("XDG_CACHE_HOME", ".cache");
    public static string DataHome() => Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"));
    public static string StateHome() => Resolve("XDG_STATE_HOME", Path.Combine(".local", "state"));

    public static string ShellyCache(params string[] parts) =>
        Path.Combine([CacheHome(), "Shelly", .. parts]);

    public static string ShellyData(params string[] parts) =>
        Path.Combine([DataHome(), "Shelly", .. parts]);

    public static string ShellyConfig(params string[] parts) =>
        Path.Combine([ConfigHome(), "shelly", .. parts]);

    public static string InvokingUserHome()
    {
        var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
        if (!string.IsNullOrEmpty(sudoUser))
            return $"/home/{sudoUser}";

        return Environment.GetEnvironmentVariable("HOME")
               ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string Resolve(string envVar, string fallbackRel)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SUDO_USER")))
        {
            var v = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(v) && Path.IsPathRooted(v))
                return v;
        }
        return Path.Combine(InvokingUserHome(), fallbackRel);
    }
}
