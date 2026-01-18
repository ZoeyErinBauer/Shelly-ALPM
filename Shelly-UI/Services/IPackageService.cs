using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Alpm;

namespace Shelly_UI.Services;

/// <summary>
/// Interface for package management operations in the UI.
/// This abstracts the communication with the privileged Shelly.Service via D-Bus.
/// </summary>
public interface IPackageService
{
    Task ConnectAsync();
    Task InitializeWithSyncAsync();
    Task InitializeAsync();
    Task SyncAsync(bool force = false);
    Task<List<AlpmPackageDto>> GetInstalledPackagesAsync();
    Task<List<AlpmPackageDto>> GetAvailablePackagesAsync();
    Task<List<AlpmPackageUpdateDto>> GetPackagesNeedingUpdateAsync();
    Task InstallPackagesAsync(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.None);
    Task RemovePackagesAsync(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.None);
    Task UpdatePackagesAsync(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.None);
    Task SyncSystemUpdateAsync(AlpmTransFlag flags = AlpmTransFlag.None);
}
