using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Alpm.Events.EventArgs;

namespace PackageManager.Alpm;

public interface IAlpmManager
{
    event EventHandler<AlpmProgressEventArgs>? Progress;
    event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;
    event EventHandler<AlpmQuestionEventArgs>? Question;
    event EventHandler<AlpmReplacesEventArgs>? Replaces;

    event EventHandler<AlpmScriptletEventArgs>? ScriptletInfo;
    event EventHandler<AlpmHookEventArgs>? HookRun;

    event EventHandler<AlpmPacnewEventArgs>? PacnewInfo;
    event EventHandler<AlpmPacsaveEventArgs>? PacsaveInfo;

    void IntializeWithSync();

    void Initialize(bool root = false, int parallelDownloads = 1, bool useTempPath = false, string tempPath = "",
        bool showHiddenPackages = false);

    void Sync(bool force = false);
    List<AlpmPackageDto> GetInstalledPackages();
    List<AlpmPackageDto> GetAvailablePackages();
    List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate();

    Task InstallPackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None);

    Task RemovePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None);

    void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None);

    Task SyncSystemUpdate(AlpmTransFlag flags = AlpmTransFlag.None);

    Task InstallLocalPackage(string path, AlpmTransFlag flags = AlpmTransFlag.None);

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
    Task InstallDependenciesOnly(string packageName, bool includeMakeDeps = false,
        AlpmTransFlag flags = AlpmTransFlag.None);

    /// <summary>
    /// Checks if a dependency is satisfied by any installed package, including via "provides" relationships.
    /// </summary>
    /// <param name="dependency">The dependency string to check (e.g., "dotnetsdk", "python>=3.10")</param>
    /// <returns>True if the dependency is satisfied by an installed package, false otherwise</returns>
    bool IsDependencySatisfiedByInstalled(string dependency);

    /// <summary>
    /// Checks if a depdency is satified by any package in the sync db
    /// </summary>
    /// <param name="depdency"></param>
    /// <returns></returns>
    bool IsDepdencySatisfiedBySyncDbs(string depdency);

    /// <summary>
    /// Finds the package name in sync databases that satisfies the given dependency string.
    /// </summary>
    /// <param name="dependency">The dependency string (e.g., "python>=3.10", "libgl")</param>
    /// <returns>The package name that satisfies the dependency, or null if not found</returns>
    string? FindSatisfierInSyncDbs(string dependency);

    void Refresh();
}