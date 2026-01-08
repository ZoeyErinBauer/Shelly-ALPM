using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using PackageManager.Models;
using PackageManager.Utilities;
using static PackageManager.Alpm.AlpmReference;

namespace PackageManager.Alpm;

public class AlpmManager(string configPath = "/etc/pacman.conf") : IDisposable
{
    private string _configPath = configPath;
    private PacmanConf _config;
    private IntPtr _handle = IntPtr.Zero;
    private static readonly HttpClient _httpClient = new();
    private AlpmDownloadCallback _downloadCallback;

    public void Initialize()
    {
        if (_handle != IntPtr.Zero)
        {
            Release(_handle);
            _handle = IntPtr.Zero;
        }

        _config = PacmanConfParser.Parse(_configPath);
        var lockFilePath = Path.Combine(_config.DbPath, "db.lck");
        if (File.Exists(lockFilePath))
        {
            try
            {
                File.Delete(lockFilePath);
            }
            catch (IOException)
            {
                //Do nothing accept natural failure
            }
        }

        _handle = AlpmReference.Initialize(_config.RootDirectory, _config.DbPath, out var error);
        if (error != 0)
        {
            Release(_handle);
            _handle = IntPtr.Zero;
            throw new Exception($"Error initializing alpm library: {error}");
        }

        if (!string.IsNullOrEmpty(_config.Architecture))
        {
            AddArchitecture(_handle, _config.Architecture);
        }

        if (!string.IsNullOrEmpty(_config.CacheDir))
        {
            AddCacheDir(_handle, _config.CacheDir);
        }

        // Resolve 'auto' architecture to the actual system architecture
        string resolvedArch = _config.Architecture;
        if (resolvedArch.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            resolvedArch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm64 => "aarch64",
                _ => "x86_64" // Fallback to a sensible default or handle other cases
            };
        }

        if (!string.IsNullOrEmpty(resolvedArch))
        {
            AddArchitecture(_handle, resolvedArch);
        }

        // Set up the download callback
        _downloadCallback = DownloadFile;
        SetDownloadCallback(_handle, _downloadCallback, IntPtr.Zero);

        foreach (var repo in _config.Repos)
        {
            IntPtr db = RegisterSyncDb(_handle, repo.Name, AlpmSigLevel.None);
            if (db != IntPtr.Zero)
            {
                SetDefaultSigLevel(_handle, AlpmSigLevel.None);
                foreach (var server in repo.Servers)
                {
                    // Resolve $repo and $arch variables in the server URL
                    string resolvedServer = server
                        .Replace("$repo", repo.Name)
                        .Replace("$arch", resolvedArch);

                    DbAddServer(db, resolvedServer);
                }
            }
        }
    }

    private int DownloadFile(IntPtr ctx, IntPtr urlPtr, IntPtr localpathPtr, int force)
    {
        try
        {
            string? url = Marshal.PtrToStringUTF8(urlPtr);
            string? localpath = Marshal.PtrToStringUTF8(localpathPtr);

            // If libalpm provides no destination, we must provide one
            if (string.IsNullOrEmpty(localpath) && !string.IsNullOrEmpty(url))
            {
                // For .db files, they usually go into DbPath
                // For .pkg files, they go into CacheDir
                string fileName = Path.GetFileName(url);
                if (url.EndsWith(".db") || url.EndsWith(".db.sig"))
                {
                    localpath = Path.Combine(_config.DbPath, "sync", fileName);
                }
                else
                {
                    localpath = Path.Combine(_config.CacheDir, fileName);
                }
            }

            if (string.IsNullOrEmpty(localpath)) return -1;

            if (File.Exists(localpath) && force == 0) return 0;

            var directory = Path.GetDirectoryName(localpath);
            if (directory != null) Directory.CreateDirectory(directory);

            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            {
                return PerformDownload(absoluteUri.ToString(), localpath);
            }

            var syncDbsPtr = GetSyncDbs(_handle);
            IntPtr targetDb = IntPtr.Zero;

            // 1. Check if the URL is a database/sig file (e.g., "core.db", "core.db.sig")
            var currentPtr = syncDbsPtr;
            while (currentPtr != IntPtr.Zero)
            {
                var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
                var dbName = Marshal.PtrToStringUTF8(DbGetName(node.Data));
                if (dbName != null && url.StartsWith(dbName))
                {
                    targetDb = node.Data;
                    break;
                }

                currentPtr = node.Next;
            }

            // 2. If it's a package, find which DB contains a package with this exact filename
            if (targetDb == IntPtr.Zero)
            {
                currentPtr = syncDbsPtr;
                while (currentPtr != IntPtr.Zero)
                {
                    var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
                    var pkgCachePtr = DbGetPkgCache(node.Data);
                    var pkgNodePtr = pkgCachePtr;

                    while (pkgNodePtr != IntPtr.Zero)
                    {
                        var pkgNode = Marshal.PtrToStructure<AlpmList>(pkgNodePtr);
                        // Get the actual filename for the package in this database
                        var pkgFilenamePtr = GetPkgFileName(pkgNode.Data);
                        var pkgFilename = Marshal.PtrToStringUTF8(pkgFilenamePtr);

                        if (pkgFilename == url)
                        {
                            targetDb = node.Data;
                            break;
                        }

                        pkgNodePtr = pkgNode.Next;
                    }

                    if (targetDb != IntPtr.Zero) break;
                    currentPtr = node.Next;
                }
            }

            // 3. Iterate through databases, prioritizing the identified targetDb
            currentPtr = syncDbsPtr;
            while (currentPtr != IntPtr.Zero)
            {
                var node = Marshal.PtrToStructure<AlpmList>(currentPtr);

                // If we found a specific DB, skip the others
                if (targetDb != IntPtr.Zero && node.Data != targetDb)
                {
                    currentPtr = node.Next;
                    continue;
                }

                var serversPtr = DbGetServers(node.Data);
                var serverNodePtr = serversPtr;
                while (serverNodePtr != IntPtr.Zero)
                {
                    var serverNode = Marshal.PtrToStructure<AlpmList>(serverNodePtr);
                    var serverBaseUrl = Marshal.PtrToStringUTF8(serverNode.Data);

                    if (!string.IsNullOrEmpty(serverBaseUrl))
                    {
                        var fullUrl = serverBaseUrl.EndsWith('/') ? $"{serverBaseUrl}{url}" : $"{serverBaseUrl}/{url}";
                        if (PerformDownload(fullUrl, localpath) == 0) return 0;
                    }

                    serverNodePtr = serverNode.Next;
                }

                if (targetDb != IntPtr.Zero) break;
                currentPtr = node.Next;
            }

            return -1;
        }
        catch (Exception ex)
        {
            var url = Marshal.PtrToStringUTF8(urlPtr);
            Console.WriteLine($"Download logic failed for {url}: {ex.Message}");
            return -1;
        }
    }

    private int PerformDownload(string fullUrl, string localpath)
    {
        try
        {
            using var response = _httpClient.GetAsync(fullUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return -1;

            using var fs = new FileStream(localpath, FileMode.Create, FileAccess.Write, FileShare.None);
            response.Content.ReadAsStream().CopyTo(fs);
            return 0;
        }
        catch
        {
            return -1;
        }
    }

    public void Sync()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        if (syncDbsPtr != IntPtr.Zero)
        {
            // Pass the entire list pointer directly to alpm_db_update
            Update(_handle, syncDbsPtr, false);
        }
    }

    public List<AlpmPackage> GetInstalledPackages()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var dbPtr = GetLocalDb(_handle);
        var pkgPtr = DbGetPkgCache(dbPtr);
        return AlpmPackage.FromList(pkgPtr);
    }

    public List<AlpmPackage> GetAvailablePackages()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var packages = new List<AlpmPackage>();
        var syncDbsPtr = GetSyncDbs(_handle);

        var currentPtr = syncDbsPtr;
        while (currentPtr != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
            if (node.Data != IntPtr.Zero)
            {
                var dbPkgCachePtr = DbGetPkgCache(node.Data);
                packages.AddRange(AlpmPackage.FromList(dbPkgCachePtr));
            }

            currentPtr = node.Next;
        }

        return packages;
    }

    public List<AlpmPackageUpdate> GetPackagesNeedingUpdate()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var packagesNeedingUpdate = new List<AlpmPackageUpdate>();
        var syncDbsPtr = GetSyncDbs(_handle);
        var installedPackages = GetInstalledPackages();

        foreach (var installedPkg in installedPackages)
        {
            var newVersionPtr = SyncGetNewVersion(installedPkg.PackagePtr, syncDbsPtr);
            if (newVersionPtr != IntPtr.Zero)
            {
                // The pointer is already an alpm_pkg_t*, no need to cast to AlpmList
                packagesNeedingUpdate.Add(new AlpmPackageUpdate(installedPkg, new AlpmPackage(newVersionPtr)));
            }
        }

        return packagesNeedingUpdate;
    }

    private string GetErrorMessage(AlpmErrno error)
    {
        return Marshal.PtrToStringUTF8(StrError(error)) ?? $"Unknown error ({error})";
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

    public void RemovePackage(string packageName,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
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

    public bool UpdateAll(AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        Update(_handle, syncDbsPtr, true);
        var localDbPtr = GetLocalDb(_handle);
        var isCompleted = true;
        try
        {
            if (TransInit(_handle, flags) == 0)
            {
                Console.WriteLine($"Failed to initialize transaction: {GetErrorMessage(ErrorNumber(_handle))}");
                isCompleted = false;
            }

            SyncSysupgrade(_handle, false);
            if (TransPrepare(_handle, out var dataPtr) != 0)
            {
                Console.WriteLine(
                    $"Failed to prepare system upgrade transaction transaction: {GetErrorMessage(ErrorNumber(_handle))}");
                isCompleted = false;
            }

            if (TransCommit(_handle, out dataPtr) != 0)
            {
                Console.WriteLine(
                    $"Failed to commit system upgrade transaction: {GetErrorMessage(ErrorNumber(_handle))}");
                isCompleted = false;
            }
        }
        finally
        {
            _ = TransRelease(_handle);
        }

        return isCompleted;
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        Release(_handle);
        _handle = IntPtr.Zero;
    }
}