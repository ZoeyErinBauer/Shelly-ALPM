using System;
using System.Collections.Generic;

namespace PackageManager.Alpm;

public interface IAlpmManager
{
    event EventHandler<AlpmProgressEventArgs>? Progress;
    event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;
    event EventHandler<AlpmQuestionEventArgs>? Question;

    void IntializeWithSync();
    void Initialize(bool root = false);
    void Sync(bool force = false);
    List<AlpmPackageDto> GetInstalledPackages();
    List<AlpmPackageDto> GetAvailablePackages();
    List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate();

    void InstallPackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);

    void RemovePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);

    void RemovePackage(string packageName, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);

    void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);

    void SyncSystemUpdate(AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks);

    void InstallLocalPackage(string path, AlpmTransFlag flags = AlpmTransFlag.None);
    
    /// <summary>
    /// This installs the first package that provides a given dependency.
    /// </summary>
    string GetPackageNameFromProvides(string provides, AlpmTransFlag flags = AlpmTransFlag.None);
}