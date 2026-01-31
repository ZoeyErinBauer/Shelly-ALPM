using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Alpm;
using PackageManager.Aur.Models;

namespace Shelly_UI.Services;

public interface IPrivilegedOperationService
{
    Task<OperationResult> SyncDatabasesAsync();
    Task<OperationResult> InstallPackagesAsync(IEnumerable<string> packages);
    Task<OperationResult> RemovePackagesAsync(IEnumerable<string> packages);
    Task<OperationResult> UpdatePackagesAsync(IEnumerable<string> packages);
    Task<OperationResult> UpgradeSystemAsync();
    Task<OperationResult> ForceSyncDatabaseAsync();
    Task<OperationResult> InstallAurPackagesAsync(IEnumerable<string> packages);
    Task<OperationResult> RemoveAurPackagesAsync(IEnumerable<string> packages);
    Task<OperationResult> UpdateAurPackagesAsync(IEnumerable<string> packages);
    Task<List<AlpmPackageUpdateDto>> GetPackagesNeedingUpdateAsync();
    Task<List<AlpmPackageDto>> GetAvailablePackagesAsync();
    Task<List<AlpmPackageDto>> GetInstalledPackagesAsync();
    Task<List<AurPackageDto>> GetAurInstalledPackagesAsync();
    Task<List<AurUpdateDto>> GetAurUpdatePackagesAsync();
    Task<List<AurPackageDto>> SearchAurPackagesAsync(string query);
}

public class OperationResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}