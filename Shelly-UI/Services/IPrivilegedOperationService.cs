using System.Collections.Generic;
using System.Threading.Tasks;

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
}

public class OperationResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}