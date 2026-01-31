using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PackageManager.Utilities;
using static PackageManager.Alpm.AlpmReference;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace PackageManager.Alpm;

[SuppressMessage("ReSharper", "SuggestVarOrType_BuiltInTypes",
    Justification = "This class should be extra clear on the type definitions of the variables.")]
[SuppressMessage("Compiler",
    "CS8618:Non-nullable field must contain a non-null value when exiting constructor. Consider adding the \'required\' modifier or declaring as nullable.")]
public partial class AlpmManager(string configPath = "/etc/pacman.conf") : IDisposable, IAlpmManager
{
    private string _configPath = configPath;
    private PacmanConf _config;
    private IntPtr _handle = IntPtr.Zero;
    private static readonly HttpClient HttpClient = new();
    private AlpmFetchCallback _fetchCallback;
    private AlpmEventCallback _eventCallback;
    private AlpmQuestionCallback _questionCallback;
    private AlpmProgressCallback? _progressCallback;

    public event EventHandler<AlpmProgressEventArgs>? Progress;
    public event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;
    public event EventHandler<AlpmQuestionEventArgs>? Question;
    public event EventHandler<AlpmReplacesEventArgs>? Replaces;

    public void IntializeWithSync()
    {
        Initialize(true);
        Sync();
    }

    public void Initialize(bool root = false)
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
                // Only try to remove it if we have root privileges, otherwise libalpm will report the lock error
                File.Delete(lockFilePath);
            }
            catch
            {
                // Ignore failures to delete lock file; libalpm will provide a proper error later
                // if it's actually locked by another process or due to permissions.
            }
        }

        _handle = AlpmReference.Initialize(_config.RootDirectory, _config.DbPath, out var error);
        if (error != 0)
        {
            Release(_handle);
            _handle = IntPtr.Zero;
            throw new Exception($"Error initializing alpm library: {error}");
        }

        if (!string.IsNullOrEmpty(_config.GpgDir) && root)
        {
            SetGpgDir(_handle, _config.GpgDir);
        }

        if (_config.SigLevel != AlpmSigLevel.None)
        {
            AlpmSigLevel sigLevel = _config.SigLevel;
            AlpmSigLevel localSigLevel = _config.LocalFileSigLevel;

            if (SetDefaultSigLevel(_handle, sigLevel) != 0)
            {
                Console.Error.WriteLine("[ALPM_ERROR] Failed to set default signature level");
            }

            if (SetLocalFileSigLevel(_handle, localSigLevel) != 0)
            {
                Console.Error.WriteLine("[ALPM_ERROR] Failed to set local file signature level");
            }
        }

        AlpmSigLevel remoteSigLevel = _config.RemoteFileSigLevel;

        if (SetRemoteFileSigLevel(_handle, remoteSigLevel) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set remote file signature level");
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
        _fetchCallback = DownloadFile;
        if (SetFetchCallback(_handle, _fetchCallback, IntPtr.Zero) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set download callback");
        }

        _eventCallback = HandleEvent;
        if (SetEventCallback(_handle, _eventCallback, IntPtr.Zero) != 0)
        {
            Console.Error.WriteLine("[ALPM_ERROR] Failed to set event callback");
        }

        _questionCallback = HandleQuestion;
        if (SetQuestionCallback(_handle, _questionCallback, IntPtr.Zero) != 0)
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
            var effectiveSigLevel = repo.SigLevel is AlpmSigLevel.None or AlpmSigLevel.UseDefault
                ? _config.SigLevel
                : repo.SigLevel;
            Console.Error.WriteLine($"[DEBUG] Registering {repo.Name} with SigLevel: {effectiveSigLevel}");
            IntPtr db = RegisterSyncDb(_handle, repo.Name, effectiveSigLevel);
            if (db == IntPtr.Zero)
            {
                var errno = ErrorNumber(_handle);
                Console.Error.WriteLine($"[ALPM_ERROR] Failed to register {repo.Name}: {errno}");
                continue;
            }

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




    public void Refresh()
    {
        if (_handle != IntPtr.Zero)
        {
            Release(_handle);
            _handle = IntPtr.Zero;
        }

        Initialize();
    }

    private string GetErrorMessage(AlpmErrno error)
    {
        return Marshal.PtrToStringUTF8(StrError(error)) ?? $"Unknown error ({error})";
    }



    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        Release(_handle);
        _handle = IntPtr.Zero;
    }

}