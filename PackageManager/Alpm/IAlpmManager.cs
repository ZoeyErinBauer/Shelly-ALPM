using System.Collections.Generic;

namespace PackageManager.Alpm;

public interface IAlpmManager
{
    void IntializeWithSync();
    void Initialize();
    void Sync(bool force = false);
    List<AlpmPackageDto> GetInstalledPackages();
    List<AlpmPackageDto> GetAvailablePackages();
    List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate();
    void InstallPackages(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);
    void RemovePackage(string packageName, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);
    void UpdatePackages(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);
}
