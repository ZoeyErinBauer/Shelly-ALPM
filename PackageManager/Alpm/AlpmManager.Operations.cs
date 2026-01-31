using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

public partial class AlpmManager
{
    public void Sync(bool force = false)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        if (syncDbsPtr != IntPtr.Zero)
        {
            // Pass the entire list pointer directly to alpm_db_update
            var result = Update(_handle, syncDbsPtr, force);
            if (result < 0)
            {
                var error = ErrorNumber(_handle);
                Console.Error.WriteLine($"Sync failed: {GetErrorMessage(error)}");
            }

            if (result > 0)
            {
                Console.Error.WriteLine($"Sync database up to date");
            }

            if (result == 0)
            {
                Console.Error.WriteLine($"Updating Sync database");
            }
        }
    }

    public List<AlpmPackageDto> GetInstalledPackages()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var dbPtr = GetLocalDb(_handle);
        var pkgPtr = DbGetPkgCache(dbPtr);
        return AlpmPackage.FromList(pkgPtr).Select(p => p.ToDto()).ToList();
    }

    public List<AlpmPackageDto> GetForeignPackages()
    {
        if (_handle == IntPtr.Zero) Initialize();

        var localDbPtr = GetLocalDb(_handle);
        var installedPkgs = AlpmPackage.FromList(DbGetPkgCache(localDbPtr));
        var syncDbsPtr = GetSyncDbs(_handle);

        var foreignPackages = new List<AlpmPackageDto>();

        foreach (var pkg in installedPkgs)
        {
            // Check if package exists in any sync database
            bool foundInSync = false;
            var currentPtr = syncDbsPtr;

            while (currentPtr != IntPtr.Zero)
            {
                var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
                if (node.Data != IntPtr.Zero)
                {
                    var syncPkgPtr = DbGetPkg(node.Data, pkg.Name);
                    if (syncPkgPtr != IntPtr.Zero)
                    {
                        foundInSync = true;
                        break;
                    }
                }

                currentPtr = node.Next;
            }

            // If not found in any sync db, it's a foreign package
            if (!foundInSync)
            {
                foreignPackages.Add(pkg.ToDto());
            }
        }

        return foreignPackages;
    }

    public List<AlpmPackageDto> GetAvailablePackages()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var packages = new List<AlpmPackageDto>();
        var syncDbsPtr = GetSyncDbs(_handle);

        var currentPtr = syncDbsPtr;
        while (currentPtr != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
            if (node.Data != IntPtr.Zero)
            {
                //Might need to swap these values
                if (DbGetValid(node.Data) != 0)
                {
                    var dbName = Marshal.PtrToStringUTF8(DbGetName(node.Data)) ?? "unknown";
                    Console.Error.WriteLine($"[ALPM_WARNING] Database '{dbName}' is invalid, skipping");
                    currentPtr = node.Next;
                    continue;
                }

                var dbPkgCachePtr = DbGetPkgCache(node.Data);
                packages.AddRange(AlpmPackage.FromList(dbPkgCachePtr).Select(p => p.ToDto()));
            }

            currentPtr = node.Next;
        }

        return packages;
    }

    public List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var updates = new List<AlpmPackageUpdateDto>();
        var syncDbsPtr = GetSyncDbs(_handle);
        var dbPtr = GetLocalDb(_handle);
        var pkgPtr = DbGetPkgCache(dbPtr);
        var installedPackages = AlpmPackage.FromList(pkgPtr);

        foreach (var installedPkg in installedPackages)
        {
            var newVersionPtr = SyncGetNewVersion(installedPkg.PackagePtr, syncDbsPtr);
            if (newVersionPtr != IntPtr.Zero)
            {
                var update = new AlpmPackageUpdate(installedPkg, new AlpmPackage(newVersionPtr));
                updates.Add(update.ToDto());
            }
        }

        return updates;
    }

    public void InstallPackage(string packageName,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        if (_handle == IntPtr.Zero) Initialize();

        // 1. Find the package in sync databases
        IntPtr pkgPtr = IntPtr.Zero;
        var syncDbsPtr = GetSyncDbs(_handle);
        var currentPtr = syncDbsPtr;
        while (currentPtr != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
            if (node.Data != IntPtr.Zero)
            {
                pkgPtr = DbGetPkg(node.Data, packageName);
                if (pkgPtr != IntPtr.Zero) break;
            }

            currentPtr = node.Next;
        }

        if (pkgPtr == IntPtr.Zero)
        {
            throw new Exception($"Package '{packageName}' not found in any sync database.");
        }

        // If we are doing a DbOnly install, we should also skip dependency checks, 
        // extraction, and signature/checksum validation to avoid requirement for the physical package file.
        if (flags.HasFlag(AlpmTransFlag.DbOnly))
        {
            flags |= AlpmTransFlag.NoDeps | AlpmTransFlag.NoExtract | AlpmTransFlag.NoPkgSig |
                     AlpmTransFlag.NoCheckSpace;
        }

        // 2. Initialize transaction
        if (TransInit(_handle, flags) != 0)
        {
            throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
        }

        try
        {
            // 3. Add package to transaction
            if (AddPkg(_handle, pkgPtr) != 0)
            {
                throw new Exception(
                    $"Failed to add package '{packageName}' to transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // 4. Prepare transaction
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception($"Failed to prepare transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // 5. Commit transaction
            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception($"Failed to commit transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        finally
        {
            // 6. Release transaction
            TransRelease(_handle);
        }
    }

    public void InstallPackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None)
    {
        if (_handle == IntPtr.Zero) Initialize();

        List<IntPtr> pkgPtrs = new List<IntPtr>();

        foreach (var packageName in packageNames)
        {
            // Find the package in sync databases
            IntPtr pkgPtr = IntPtr.Zero;
            var syncDbsPtr = GetSyncDbs(_handle);
            var currentPtr = syncDbsPtr;
            while (currentPtr != IntPtr.Zero)
            {
                var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
                if (node.Data != IntPtr.Zero)
                {
                    pkgPtr = DbGetPkg(node.Data, packageName);
                    if (pkgPtr != IntPtr.Zero) break;
                }

                currentPtr = node.Next;
            }

            if (pkgPtr == IntPtr.Zero)
            {
                throw new Exception($"Package '{packageName}' not found in any sync database.");
            }

            pkgPtrs.Add(pkgPtr);
        }

        if (pkgPtrs.Count == 0) return;

        // If we are doing a DbOnly install, we should also skip dependency checks, 
        // extraction, and signature/checksum validation to avoid requirement for the physical package file.
        if (flags.HasFlag(AlpmTransFlag.DbOnly))
        {
            flags |= AlpmTransFlag.NoDeps | AlpmTransFlag.NoExtract | AlpmTransFlag.NoPkgSig |
                     AlpmTransFlag.NoCheckSpace;
        }

        // Initialize transaction
        if (TransInit(_handle, flags) != 0)
        {
            throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
        }

        try
        {
            foreach (var pkgPtr in pkgPtrs)
            {
                if (AddPkg(_handle, pkgPtr) != 0)
                {
                    // Note: In libalpm, if one fails, we might want to know which one, 
                    // but here we just throw an exception for the first failure.
                    throw new Exception(
                        $"Failed to add a package to transaction: {GetErrorMessage(ErrorNumber(_handle))}");
                }
            }

            // Prepare transaction
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception($"Failed to prepare transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // Commit transaction
            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception($"Failed to commit transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        finally
        {
            // Release transaction
            TransRelease(_handle);
        }
    }

    public void RemovePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None)
    {
        if (_handle == IntPtr.Zero) Initialize();

        List<IntPtr> pkgPtrs = new List<IntPtr>();
        var localDbPtr = GetLocalDb(_handle);
        foreach (var packageName in packageNames)
        {
            // Find the package in sync databases

            var pkgPtr = DbGetPkg(localDbPtr, packageName);

            if (pkgPtr == IntPtr.Zero)
            {
                throw new Exception($"Package '{packageName}' not found in any sync database.");
            }

            pkgPtrs.Add(pkgPtr);
        }

        if (pkgPtrs.Count == 0) return;

        // If we are doing a DbOnly install, we should also skip dependency checks, 
        // extraction, and signature/checksum validation to avoid requirement for the physical package file.
        if (flags.HasFlag(AlpmTransFlag.DbOnly))
        {
            flags |= AlpmTransFlag.NoDeps | AlpmTransFlag.NoExtract | AlpmTransFlag.NoPkgSig |
                     AlpmTransFlag.NoCheckSpace;
        }

        // Initialize transaction
        if (TransInit(_handle, flags) != 0)
        {
            throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
        }

        try
        {
            foreach (var pkgPtr in pkgPtrs)
            {
                if (RemovePkg(_handle, pkgPtr) != 0)
                {
                    // Note: In libalpm, if one fails, we might want to know which one, 
                    // but here we just throw an exception for the first failure.
                    throw new Exception(
                        $"Failed to add a package to transaction: {GetErrorMessage(ErrorNumber(_handle))}");
                }
            }

            // Prepare transaction
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception($"Failed to prepare transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // Commit transaction
            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception($"Failed to commit transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        finally
        {
            // Release transaction
            TransRelease(_handle);
        }
    }

    public void RemovePackage(string packageName,
        AlpmTransFlag flags = AlpmTransFlag.None)
    {
        if (_handle == IntPtr.Zero) Initialize();

        // 1. Find the package in the local database
        var localDbPtr = GetLocalDb(_handle);
        var pkgPtr = DbGetPkg(localDbPtr, packageName);

        if (pkgPtr == IntPtr.Zero)
        {
            throw new Exception($"Package '{packageName}' not found in the local database.");
        }

        // 2. Initialize transaction
        // Using 0 for flags for now, similar to InstallPackage
        if (TransInit(_handle, flags) != 0)
        {
            throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
        }

        try
        {
            // 3. Add package to removal list
            if (RemovePkg(_handle, pkgPtr) != 0)
            {
                throw new Exception(
                    $"Failed to add package '{packageName}' to removal transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // 4. Prepare transaction
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception($"Failed to prepare removal transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // 5. Commit transaction
            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception($"Failed to commit removal transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        finally
        {
            // 6. Release transaction
            TransRelease(_handle);
        }
    }

    public void SyncSystemUpdate(AlpmTransFlag flags = AlpmTransFlag.None)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        Update(_handle, syncDbsPtr, true);
        try
        {
            if (TransInit(_handle, flags) != 0)
            {
                throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            if (SyncSysupgrade(_handle, false) != 0) throw new Exception(GetErrorMessage(ErrorNumber(_handle)));
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to prepare system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            CheckTransactionReplaces(_handle);

            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to commit system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize transaction: {ex.Message}");
        }
        finally
        {
            _ = TransRelease(_handle);
        }
    }

    private void CheckTransactionReplaces(IntPtr handle)
    {
        var addList = TransGetAdd(handle);
        if (addList == IntPtr.Zero) return;

        var packages = AlpmPackage.FromList(addList);
        foreach (var pkg in packages)
        {
            var replaces = pkg.Replaces;
            if (replaces.Count > 0)
            {
                Replaces?.Invoke(this, new AlpmReplacesEventArgs(pkg.Name, pkg.Repository, replaces));
            }
        }
    }

    public void InstallLocalPackage(string path, AlpmTransFlag flags = AlpmTransFlag.None)
    {
        if (_handle == IntPtr.Zero) Initialize();

        // 1. Load package from file
        var result = PkgLoad(_handle, path, true, AlpmSigLevel.PackageOptional | AlpmSigLevel.DatabaseOptional,
            out IntPtr pkgPtr);
        if (result != 0 || pkgPtr == IntPtr.Zero)
        {
            throw new Exception($"Failed to load package from '{path}': {GetErrorMessage(ErrorNumber(_handle))}");
        }

        // 2. Initialize transaction
        if (TransInit(_handle, flags) != 0)
        {
            _ = PkgFree(pkgPtr);
            throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
        }

        try
        {
            // 3. Add package to transaction
            if (AddPkg(_handle, pkgPtr) != 0)
            {
                _ = PkgFree(pkgPtr);
                throw new Exception($"Failed to add package to transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // 4. Prepare transaction
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception($"Failed to prepare transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // 5. Commit transaction
            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception($"Failed to commit transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        catch (Exception ex)
        {
            _ = PkgFree(pkgPtr);
            throw new Exception($"Failed to initialize transaction: {ex.Message}");
        }
        finally
        {
            TransRelease(_handle);
            Refresh();
        }
    }

    public string GetPackageNameFromProvides(string provides, AlpmTransFlag flags = AlpmTransFlag.None)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        var currentPtr = syncDbsPtr;
        while (currentPtr != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
            if (node.Data != IntPtr.Zero)
            {
                //Grab pkg cache
                var pkgCache = DbGetPkgCache(node.Data);
                var pkgPtr = PkgFindSatisfier(pkgCache, provides);
                if (pkgPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringUTF8(GetPkgName(pkgPtr))!;
                }
            }

            currentPtr = node.Next;
        }

        return string.Empty;
    }

    public void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None)
    {
        List<IntPtr> pkgPtrs = [];
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        var localDbPtr = GetLocalDb(_handle);
        Update(_handle, syncDbsPtr, true);
        try
        {
            foreach (var packageName in packageNames)
            {
                IntPtr installedPkgPtr = DbGetPkg(localDbPtr, packageName);
                if (installedPkgPtr == IntPtr.Zero)
                {
                    //Don't attempt to update something that doesn't exist.
                    continue;
                }

                // Find the package in sync databases
                IntPtr pkgPtr = SyncGetNewVersion(installedPkgPtr, syncDbsPtr);

                if (pkgPtr == IntPtr.Zero)
                {
                    continue;
                }

                pkgPtrs.Add(pkgPtr);
            }

            if (TransInit(_handle, flags) != 0)
            {
                throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            foreach (var pkgPtr in pkgPtrs)
            {
                _ = AddPkg(_handle, pkgPtr);
            }

            // Check if there are any packages to add or remove before preparing/committing
            if (TransGetAdd(_handle) == IntPtr.Zero && TransGetRemove(_handle) == IntPtr.Zero)
            {
                return; // Nothing to do, considered successful
            }

            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to prepare system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to commit system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }
        }
        finally
        {
            _ = TransRelease(_handle);
        }
    }

    public bool UpdateSinglePackage(string packageName,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        Update(_handle, syncDbsPtr, true);
        try
        {
            if (TransInit(_handle, flags) != 0)
            {
                throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            var pkgPtr = DbGetPkg(GetLocalDb(_handle), packageName);
            if (AddPkg(_handle, pkgPtr) != 0)
            {
                throw new Exception($"Failed to mark system upgrade: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // Check if there are any packages to add or remove before preparing/committing
            if (TransGetAdd(_handle) == IntPtr.Zero && TransGetRemove(_handle) == IntPtr.Zero)
            {
                return true; // Nothing to do, considered successful
            }

            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to prepare system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to commit system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            return true;
        }
        finally
        {
            _ = TransRelease(_handle);
        }
    }

    public bool UpdateAll(AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        Update(_handle, syncDbsPtr, true);
        try
        {
            if (TransInit(_handle, flags) != 0)
            {
                throw new Exception($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            if (SyncSysupgrade(_handle, false) != 0)
            {
                throw new Exception($"Failed to mark system upgrade: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            // Check if there are any packages to add or remove before preparing/committing
            if (TransGetAdd(_handle) == IntPtr.Zero && TransGetRemove(_handle) == IntPtr.Zero)
            {
                return true; // Nothing to do, considered successful
            }

            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to prepare system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            if (TransCommit(_handle, out dataPtr) != 0)
            {
                throw new Exception(
                    $"Failed to commit system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
            }

            return true;
        }
        finally
        {
            _ = TransRelease(_handle);
        }
    }
}
