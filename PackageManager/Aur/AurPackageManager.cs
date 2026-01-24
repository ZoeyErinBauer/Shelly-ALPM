using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Alpm;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

/// <summary>
/// This is a manager for Arch universal repositories. It relies on <see cref="AlpmManager"/> to handle downloading and
/// installation of packages from the Arch User Repository (AUR).
/// </summary>
public class AurPackageManager(string? configPath = null) : IAurPackageManager
{
    private AlpmManager _alpm;
    private AurSearchManager _aurSearchManager;

    public Task Initialize(bool root = false)
    {
        _alpm = configPath is null ? new AlpmManager() : new AlpmManager(configPath);
        _alpm.Initialize(root);
        return Task.CompletedTask;
    }

    public Task<List<AurPackageDto>> GetInstalledPackages()
    {
        throw new System.NotImplementedException();
    }

    public Task<List<AurPackageDto>> SearchPackages(string query)
    {
        throw new System.NotImplementedException();
    }

    public Task<List<AurPackageDto>> GetPackagesNeedingUpdate()
    {
        throw new System.NotImplementedException();
    }

    public Task UpdatePackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }

    public Task InstallPackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }

    public Task RemovePackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }
}