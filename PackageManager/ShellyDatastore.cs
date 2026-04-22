using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using PackageManager.Alpm.Pacfile;

namespace PackageManager;

/// <summary>
/// Manages storage locations for Shelly only storage. This is to be used to support features that don't exist
/// inside pacman.
/// </summary>
public static class ShellyDatastore
{
    private const string PacfileStoreFile = "pacfiles.tar";
    private const string ConfigPath = "/var/lib/shelly";

    public static string GetPacfileStoragePath()
    {
        Directory.CreateDirectory(ConfigPath);
        return Path.Combine(ConfigPath, PacfileStoreFile);
    }
}