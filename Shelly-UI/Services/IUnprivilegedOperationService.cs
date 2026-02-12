using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Flatpak;
using Shelly_UI.Models;

namespace Shelly_UI.Services;

public interface IUnprivilegedOperationService
{
    Task<UnprivilegedOperationResult> RemoveFlatpakPackage(IEnumerable<string> packages);
    Task<List<FlatpakPackageDto>> ListFlatpakPackages();

    Task<List<FlatpakPackageDto>> ListFlatpakUpdates();

    Task<List<FlatpakPackageDto>> ListAppstreamFlatpak();

    Task<UnprivilegedOperationResult> FlatpakUpgrade();

    Task<UnprivilegedOperationResult> UpdateFlatpakPackage(string package);

    Task<UnprivilegedOperationResult> RemoveFlatpakPackage(string package);

    Task<UnprivilegedOperationResult> InstallFlatpakPackage(string package);
    
    Task<UnprivilegedOperationResult> FlatpakSyncRemoteAppstream();
}

public class UnprivilegedOperationResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}