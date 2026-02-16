using System;
using System.Collections.Generic;

namespace PackageManager.Alpm;

public interface IAlpmManager
{
    event EventHandler<AlpmProgressEventArgs>? Progress;
    event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;
    event EventHandler<AlpmQuestionEventArgs>? Question;
    event EventHandler<AlpmReplacesEventArgs>? Replaces;

    void IntializeWithSync();
    void Initialize(bool root = false, bool useTempPath = false, string tempPath = "");
    void Sync(bool force = false);
    List<AlpmPackageDto> GetInstalledPackages();
    List<AlpmPackageDto> GetAvailablePackages();
    List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate();

    void InstallPackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None);

    void RemovePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None);

    void RemovePackage(string packageName, AlpmTransFlag flags = AlpmTransFlag.None);

    void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None);

    void SyncSystemUpdate(AlpmTransFlag flags = AlpmTransFlag.None);

    void InstallLocalPackage(string path, AlpmTransFlag flags = AlpmTransFlag.None);

    /// <summary>
    /// This installs the first package that provides a given dependency.
    /// </summary>
    string GetPackageNameFromProvides(string provides, AlpmTransFlag flags = AlpmTransFlag.None);

    /// <summary>
    /// This installs package dependencies only for a given package.
    /// </summary>
    /// <param name="packageName">Name of the package that dependencies are being installed for</param>
    /// <param name="includeMakeDeps"></param>
    /// <param name="flags">Flags that should be used for the installation</param>
    void InstallDependenciesOnly(string packageName,bool includeMakeDeps = false,
        AlpmTransFlag flags = AlpmTransFlag.None);

    /// <summary>
    /// Checks if a dependency is satisfied by any installed package, including via "provides" relationships.
    /// </summary>
    /// <param name="dependency">The dependency string to check (e.g., "dotnetsdk", "python>=3.10")</param>
    /// <returns>True if the dependency is satisfied by an installed package, false otherwise</returns>
    bool IsDependencySatisfiedByInstalled(string dependency);

    void Refresh();
}