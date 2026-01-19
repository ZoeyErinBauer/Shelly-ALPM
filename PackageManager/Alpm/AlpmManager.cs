using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PackageManager.Utilities;
using static PackageManager.Alpm.AlpmReference;

namespace PackageManager.Alpm;

public class AlpmManager(string configPath = "/etc/pacman.conf") : IDisposable, IAlpmManager
{
    private string _configPath = configPath;
    private PacmanConf _config;
    private IntPtr _handle = IntPtr.Zero;
    private static readonly HttpClient _httpClient = new();
    private AlpmDownloadCallback _downloadCallback;
    private AlpmEventCallback _eventCallback;
    private AlpmQuestionCallback _questionCallback;
    private AlpmProgressCallback? _progressCallback;

    public event EventHandler<AlpmProgressEventArgs>? Progress;
    public event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;
    public event EventHandler<AlpmQuestionEventArgs>? Question;

    public void IntializeWithSync()
    {
        Initialize();
        Sync();
    }

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

        if (!string.IsNullOrEmpty(_config.CacheDir))
        {
            AddCacheDir(_handle, _config.CacheDir);
        }

        //Resolve 'auto' architecture to the actual system architecture
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
            AddArchitecture(_handle, "any");
        }

        // Set up the download callback
        _downloadCallback = DownloadFile;
        if (SetDownloadCallback(_handle, _downloadCallback, IntPtr.Zero) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set download callback");
        }

        _eventCallback = HandleEvent;
        if (SetEventCallback(_handle, _eventCallback) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set event callback");
        }

        _questionCallback = HandleQuestion;
        if (SetQuestionCallback(_handle, _questionCallback) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set question callback");
        }

        _progressCallback = HandleProgress;
        if (SetProgressCallback(_handle, _progressCallback, IntPtr.Zero) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set progress callback");
        }

        foreach (var repo in _config.Repos)
        {
            IntPtr db = RegisterSyncDb(_handle, repo.Name, AlpmSigLevel.None);
            if (db != IntPtr.Zero)
            {
                SetDefaultSigLevel(_handle, AlpmSigLevel.None);
                foreach (var server in repo.Servers)
                {
                    var archSuffixMatch = Regex.Match(server, @"\$arch([^/]+)");
                    if (archSuffixMatch.Success)
                    {
                        string suffix = archSuffixMatch.Groups[1].Value;
                        AddArchitecture(_handle, resolvedArch + suffix);
                        //Commented out logs because it's too much noise. Uncomment if needed
                        //Console.Error.WriteLine($"[DEBUG_LOG] Found architecture suffix: {suffix}");
                        //Console.Error.WriteLine($"[DEBUG_LOG] Registering Architecture: {resolvedArch + suffix}");
                    }

                    // Resolve $repo and $arch variables in the server URL
                    var resolvedServer = server
                        .Replace("$repo", repo.Name)
                        .Replace("$arch", resolvedArch);
                    //Console.Error.WriteLine($"[DEBUG_LOG] Resolved Architecture {resolvedArch}");

                    //Console.Error.WriteLine($"[DEBUG_LOG] Registering Server: {resolvedServer}");
                    DbAddServer(db, resolvedServer);
                }
            }
        }
    }

    private void HandleQuestion(IntPtr questionPtr)
    {
        var question = Marshal.PtrToStructure<AlpmQuestionAny>(questionPtr);
        var questionType = (AlpmQuestionType)question.Type;

        var questionText = questionType switch
        {
            AlpmQuestionType.InstallIgnorePkg => "Install IgnorePkg?",
            AlpmQuestionType.ReplacePkg => "Replace package?",
            AlpmQuestionType.ConflictPkg => "Conflict found. Remove?",
            AlpmQuestionType.CorruptedPkg => "Corrupted pkg. Delete?",
            AlpmQuestionType.ImportKey => "Import GPG key?",
            AlpmQuestionType.SelectProvider => "Select provider?",
            _ => $"Unknown question type: {question.Type}"
        };

        var args = new AlpmQuestionEventArgs(questionType, questionText);
        Question?.Invoke(this, args);

        Console.Error.WriteLine($"[ALPM_QUESTION] {questionText} (Answering {args.Response})");

        // Write the response back to the answer field.
        question.Answer = args.Response;
        Marshal.StructureToPtr(question, questionPtr, false);
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
                    //Console.WriteLine($"Using {localpath} as destination for {url}");
                    Console.Error.WriteLine($"[DEBUG_LOG] Using {localpath} as destination for {url}");
                }
                else
                {
                    localpath = Path.Combine(_config.CacheDir, fileName);
                    //Console.WriteLine($"Using {localpath} as destination for {url}");
                    Console.Error.WriteLine($"[DEBUG_LOG] Using {localpath} as destination for {url}");
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
            string urlString;
            try
            {
                urlString = Marshal.PtrToStringUTF8(urlPtr) ?? "unknown url";
            }
            catch
            {
                urlString = "invalid pointer";
            }

            //Console.WriteLine($"Download logic failed for {urlString}: {ex.Message}");
            //Console.Error.WriteLine($"[DEBUG_LOG] Download logic failed for {urlString}: {ex.Message}");
            return -1;
        }
    }

    private int PerformDownload(string fullUrl, string localpath)
    {
        try
        {
            Console.Error.WriteLine($"[DEBUG_LOG] Downloading {fullUrl} to {localpath}");
            using var response = _httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter()
                .GetResult();
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[DEBUG_LOG] Failed to download {fullUrl}: {response.StatusCode}");
                return -1;
            }

            var totalBytes = response.Content.Headers.ContentLength;
            string fileName = Path.GetFileName(localpath);

            try
            {
                using var fs = new FileStream(localpath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                using var stream = response.Content.ReadAsStream();

                byte[] buffer = new byte[8192];
                int bytesRead;
                long totalRead = 0;
                int lastPercent = -1;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        int percent = (int)((totalRead * 100) / totalBytes.Value);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            Progress?.Invoke(this, new AlpmProgressEventArgs(
                                AlpmProgressType.PackageDownload,
                                fileName,
                                percent,
                                (ulong)totalBytes.Value,
                                (ulong)totalRead
                            ));
                        }
                    }
                }

                // Ensure 100% is sent
                if (lastPercent != 100)
                {
                    Progress?.Invoke(this, new AlpmProgressEventArgs(
                        AlpmProgressType.PackageDownload,
                        fileName,
                        100,
                        (ulong)(totalBytes ?? (long)totalRead),
                        (ulong)totalRead
                    ));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DEBUG_LOG] Failed to write to {localpath}: {ex.Message}");
                return -1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DEBUG_LOG] Download failed for {fullUrl}: {ex.Message}");
            return -1;
        }
    }

    public void Sync(bool force = false)
    {
        if (_handle == IntPtr.Zero) Initialize();
        var syncDbsPtr = GetSyncDbs(_handle);
        if (syncDbsPtr != IntPtr.Zero)
        {
            // Pass the entire list pointer directly to alpm_db_update
            Update(_handle, syncDbsPtr, force);
        }
    }

    public List<AlpmPackageDto> GetInstalledPackages()
    {
        if (_handle == IntPtr.Zero) Initialize();
        var dbPtr = GetLocalDb(_handle);
        var pkgPtr = DbGetPkgCache(dbPtr);
        return AlpmPackage.FromList(pkgPtr).Select(p => p.ToDto()).ToList();
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

    public void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.None)
    {
        List<IntPtr> pkgPtrs = [];
        List<IntPtr> failedPkgPtrs = [];
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
                IntPtr pkgPtr = IntPtr.Zero;
                pkgPtr = SyncGetNewVersion(installedPkgPtr, syncDbsPtr);
                // var currentPtr = syncDbsPtr;
                // while (currentPtr != IntPtr.Zero)
                // {
                //     var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
                //     if (node.Data != IntPtr.Zero)
                //     {
                //         pkgPtr = DbGetPkg(node.Data, packageName);
                //         if (pkgPtr != IntPtr.Zero) break;
                //     }
                //
                //     currentPtr = node.Next;
                // }

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

            failedPkgPtrs.AddRange(pkgPtrs.Where(pkgPtr => AddPkg(_handle, pkgPtr) != 0));

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

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        Release(_handle);
        _handle = IntPtr.Zero;
    }

    private void HandleProgress(IntPtr ctx, AlpmProgressType progress, IntPtr pkgNamePtr, int percent, ulong howmany,
        ulong current)
    {
        try
        {
            string? pkgName = pkgNamePtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(pkgNamePtr) : null;
            Console.Error.WriteLine($"[DEBUG_LOG] ALPM Progress: {progress}, Pkg: {pkgName}, %: {percent}");

            Progress?.Invoke(this, new AlpmProgressEventArgs(
                progress,
                pkgName,
                percent,
                howmany,
                current
            ));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ALPM_ERROR] Error in progress callback: {ex.Message}");
        }
    }

    private void HandleEvent(IntPtr eventPtr)
    {
        if (eventPtr == IntPtr.Zero) return;

        try
        {
            // Read the type field directly using ReadInt32 - this is safer than PtrToStructure
            // for the initial type detection as it avoids potential marshalling issues
            var type = (AlpmEventType)Marshal.ReadInt32(eventPtr);

            switch (type)
            {
                case AlpmEventType.CheckDepsStart:
                    Console.Error.WriteLine("[ALPM] Checking dependencies...");
                    break;
                case AlpmEventType.CheckDepsDone:
                    Console.Error.WriteLine("[ALPM] Dependency check finished.");
                    break;
                case AlpmEventType.FileConflictsStart:
                    Console.Error.WriteLine("[ALPM] Checking for file conflicts...");
                    break;
                case AlpmEventType.FileConflictsDone:
                    Console.Error.WriteLine("[ALPM] File conflict check finished.");
                    break;
                case AlpmEventType.ResolveDepsStart:
                    Console.Error.WriteLine("[ALPM] Resolving dependencies...");
                    break;
                case AlpmEventType.ResolveDepsDone:
                    Console.Error.WriteLine("[ALPM] Dependency resolution finished.");
                    break;
                case AlpmEventType.InterConflictsStart:
                    Console.Error.WriteLine("[ALPM] Checking for inter-conflicts...");
                    break;
                case AlpmEventType.InterConflictsDone:
                    Console.Error.WriteLine("[ALPM] Inter-conflict check finished.");
                    break;
                case AlpmEventType.TransactionStart:
                    Console.Error.WriteLine("[ALPM] Starting transaction...");
                    PackageOperation?.Invoke(this, new AlpmPackageOperationEventArgs(type, null));
                    break;
                case AlpmEventType.TransactionDone:
                    Console.Error.WriteLine("[ALPM] Transaction successfully finished.");
                    PackageOperation?.Invoke(this, new AlpmPackageOperationEventArgs(type, null));
                    break;
                case AlpmEventType.IntegrityStart:
                    Console.Error.WriteLine("[ALPM] Checking package integrity...");
                    break;
                case AlpmEventType.IntegrityDone:
                    Console.Error.WriteLine("[ALPM] Integrity check finished.");
                    break;
                case AlpmEventType.LoadStart:
                    Console.Error.WriteLine("[ALPM] Loading packages...");
                    break;
                case AlpmEventType.LoadDone:
                    Console.Error.WriteLine("[ALPM] Packages loaded.");
                    break;
                case AlpmEventType.DiskspaceStart:
                    Console.Error.WriteLine("[ALPM] Checking available disk space...");
                    break;
                case AlpmEventType.DiskspaceDone:
                    Console.Error.WriteLine("[ALPM] Disk space check finished.");
                    break;

                case AlpmEventType.PackageOperationStart:
                {
                    // PackageOperation: type (4 bytes) + operation (4 bytes) + oldpkg ptr + newpkg ptr
                    // Offset 8 for first pointer (type + operation = 8 bytes, no padding needed)
                    const int ptrOffset = 8;
                    IntPtr oldPkgPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset);
                    IntPtr newPkgPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset + IntPtr.Size);
                    
                    // For install/upgrade, use NewPkgPtr; for remove, use OldPkgPtr
                    IntPtr pkgPtr = newPkgPtr != IntPtr.Zero ? newPkgPtr : oldPkgPtr;
                    string? pkgName = pkgPtr != IntPtr.Zero 
                        ? Marshal.PtrToStringUTF8(GetPkgName(pkgPtr)) 
                        : null;
                    Console.Error.WriteLine($"[ALPM] Processing package: {pkgName}");
                    PackageOperation?.Invoke(this, new AlpmPackageOperationEventArgs(type, pkgName));
                    break;
                }

                case AlpmEventType.PackageOperationDone:
                {
                    const int ptrOffset = 8;
                    IntPtr oldPkgPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset);
                    IntPtr newPkgPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset + IntPtr.Size);
                    
                    IntPtr pkgPtr = newPkgPtr != IntPtr.Zero ? newPkgPtr : oldPkgPtr;
                    string? pkgName = pkgPtr != IntPtr.Zero 
                        ? Marshal.PtrToStringUTF8(GetPkgName(pkgPtr)) 
                        : null;
                    Console.Error.WriteLine($"[ALPM] Finished processing: {pkgName}");
                    PackageOperation?.Invoke(this, new AlpmPackageOperationEventArgs(type, pkgName));
                    break;
                }

                case AlpmEventType.ScriptletInfo:
                {
                    // ScriptletInfo: type (4 bytes) + padding (4 bytes on 64-bit) + line pointer
                    // On 64-bit, pointer must be 8-byte aligned, so offset is 8
                    const int ptrOffset = 8;
                    IntPtr linePtr = Marshal.ReadIntPtr(eventPtr, ptrOffset);
                    string? msg = linePtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(linePtr) : null;
                    if (!string.IsNullOrEmpty(msg))
                    {
                        Console.Error.WriteLine($"[ALPM_SCRIPT] {msg.Trim()}");
                    }
                    break;
                }

                case AlpmEventType.HookStart:
                    Console.Error.WriteLine("[ALPM] Running hooks...");
                    break;
                case AlpmEventType.HookDone:
                    Console.Error.WriteLine("[ALPM] Hooks finished.");
                    break;

                case AlpmEventType.HookRunStart:
                {
                    // HookRun: type (4 bytes) + padding (4 bytes) + name ptr + desc ptr + position + total
                    const int ptrOffset = 8;
                    IntPtr namePtr = Marshal.ReadIntPtr(eventPtr, ptrOffset);
                    IntPtr descPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset + IntPtr.Size);
                    string? hookName = namePtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(namePtr) : null;
                    string? hookDesc = descPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(descPtr) : null;
                    Console.Error.WriteLine($"[ALPM_HOOK] Running: {hookDesc ?? hookName}");
                    break;
                }
                case AlpmEventType.HookRunDone:
                    Console.Error.WriteLine("[ALPM] Hook finished.");
                    break;

                // Database retrieval events (for sync operations)
                case AlpmEventType.DbRetrieveStart:
                    Console.Error.WriteLine("[ALPM] Retrieving database...");
                    break;
                case AlpmEventType.DbRetrieveDone:
                    Console.Error.WriteLine("[ALPM] Database retrieved.");
                    break;
                case AlpmEventType.DbRetrieveFailed:
                    Console.Error.WriteLine("[ALPM] Database retrieval failed.");
                    break;

                // Package retrieval events
                case AlpmEventType.PkgRetrieveStart:
                    Console.Error.WriteLine("[ALPM] Retrieving packages...");
                    break;
                case AlpmEventType.PkgRetrieveDone:
                    Console.Error.WriteLine("[ALPM] Packages retrieved.");
                    break;
                case AlpmEventType.PkgRetrieveFailed:
                    Console.Error.WriteLine("[ALPM] Package retrieval failed.");
                    break;

                case AlpmEventType.DatabaseMissing:
                {
                    // DatabaseMissing: type (4 bytes) + padding (4 bytes) + dbname ptr
                    const int ptrOffset = 8;
                    IntPtr dbNamePtr = Marshal.ReadIntPtr(eventPtr, ptrOffset);
                    string? dbName = dbNamePtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(dbNamePtr) : null;
                    Console.Error.WriteLine($"[ALPM] Database missing: {dbName}");
                    break;
                }

                case AlpmEventType.OptdepRemoval:
                    Console.Error.WriteLine("[ALPM] Optional dependency being removed.");
                    break;

                case AlpmEventType.KeyringStart:
                    Console.Error.WriteLine("[ALPM] Checking keyring...");
                    break;
                case AlpmEventType.KeyringDone:
                    Console.Error.WriteLine("[ALPM] Keyring check finished.");
                    break;
                case AlpmEventType.KeyDownloadStart:
                    Console.Error.WriteLine("[ALPM] Downloading keys...");
                    break;
                case AlpmEventType.KeyDownloadDone:
                    Console.Error.WriteLine("[ALPM] Key download finished.");
                    break;

                case AlpmEventType.PacnewCreated:
                    Console.Error.WriteLine("[ALPM] .pacnew file created.");
                    break;
                case AlpmEventType.PacsaveCreated:
                    Console.Error.WriteLine("[ALPM] .pacsave file created.");
                    break;

                default:
                    // Fallback for types we haven't explicitly handled yet
                    // Console.Error.WriteLine($"[ALPM_DEBUG] Event Triggered: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ALPM_ERROR] Error handling event: {ex.Message}");
        }
    }
}