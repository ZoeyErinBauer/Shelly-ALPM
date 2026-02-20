using PackageManager.Alpm;
using PackageManager.Aur;
using PackageManager.Aur.Models;
using PackageManager.Flatpak;

namespace Shelly_Notifications.Services;

public abstract class UpdateService
{
    public static async Task<int> CheckForUpdates()
    {
        var alpmManager = new AlpmManager();
        var aurManager = new AurPackageManager();
        var flatPakManager = new FlatpakManager();
        
        var username = Environment.GetEnvironmentVariable("USER");
        var dbPath = Path.Combine("/home", username, ".cache", "Shelly", "db");
        Directory.CreateDirectory(dbPath);
        
        alpmManager.Initialize(false, true, dbPath);
        alpmManager.Sync();
        
        var alpmPackages = alpmManager.GetPackagesNeedingUpdate();
        alpmManager.Dispose();

        aurManager.Initialize(false, true, dbPath);
        var aurPackages = await aurManager.GetPackagesNeedingUpdate();

        var flatpakPackages = flatPakManager.GetPackagesWithUpdates();

        return alpmPackages.Count + aurPackages.Count + flatpakPackages.Count;
    }
}