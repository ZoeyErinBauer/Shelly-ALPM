using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PackageManager.Flatpak;

public class FlatpakManager
{
    /// <summary>
    /// Searches installed flatpak apps
    /// </summary>
    /// <returns>Returns a list of FlatpakPackageDto</returns>
    public List<FlatpakPackageDto> SearchInstalled()
    {
        var packages = new List<FlatpakPackageDto>();

        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return packages;
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            var length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (var i = 0; i < length; i++)
            {
                var installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                var refsPtr = FlatpakReference.InstallationListInstalledRefs(
                    installationPtr, IntPtr.Zero, out IntPtr refsError);

                if (refsError != IntPtr.Zero || refsPtr == IntPtr.Zero)
                {
                    FlatpakReference.GObjectUnref(installationPtr);
                    continue;
                }

                try
                {
                    var refsDataPtr = Marshal.ReadIntPtr(refsPtr);
                    var refsLength = Marshal.ReadInt32(refsPtr + IntPtr.Size);

                    for (var j = 0; j < refsLength; j++)
                    {
                        var refPtr = Marshal.ReadIntPtr(refsDataPtr + j * IntPtr.Size);
                        if (refPtr == IntPtr.Zero) continue;

                        var package = new FlatpackPackage(refPtr);
                        packages.Add(package.ToDto());
                    }
                }
                finally
                {
                    FlatpakReference.GPtrArrayUnref(refsPtr);
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }

        return packages;
    }

    /// <summary>
    /// Launches a flatpak application by its app ID or friendly name.
    /// </summary>
    /// <param name="nameOrId">The application ID (e.g., "org.mozilla.firefox") or friendly name (e.g., "Firefox")</param>
    /// <returns>True if launch was successful</returns>
    public bool LaunchApp(string nameOrId)
    {
        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            var length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (var i = 0; i < length; i++)
            {
                var installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                var match = FindInstalledApp(installationPtr, nameOrId);

                if (match == null) continue;
                var success = FlatpakReference.InstallationLaunch(
                    installationPtr,
                    match.Id,
                    match.Arch,
                    match.Branch,
                    null, // commit
                    IntPtr.Zero, // cancellable
                    out var launchError);

                if (success && launchError == IntPtr.Zero)
                {
                    return true;
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }

        return false;
    }

    /// <summary>
    /// Finds an installed app by ID or friendly name within an installation.
    /// </summary>
    private FlatpakPackageDto? FindInstalledApp(IntPtr installationPtr, string nameOrId)
    {
        var refsPtr = FlatpakReference.InstallationListInstalledRefs(
            installationPtr, IntPtr.Zero, out IntPtr refsError);

        if (refsError != IntPtr.Zero || refsPtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var refsDataPtr = Marshal.ReadIntPtr(refsPtr);
            var refsLength = Marshal.ReadInt32(refsPtr + IntPtr.Size);

            for (var j = 0; j < refsLength; j++)
            {
                var refPtr = Marshal.ReadIntPtr(refsDataPtr + j * IntPtr.Size);
                if (refPtr == IntPtr.Zero) continue;

                var package = new FlatpackPackage(refPtr);

                if (string.Equals(package.Id, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                    package.Id.Contains(nameOrId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(package.Name, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                    package.Name.Contains(nameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    return package.ToDto();
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(refsPtr);
        }

        return null;
    }

    /// <summary>
    /// Kills a running flatpak application by its app ID.
    /// </summary>
    /// <param name="appId">The application ID to kill</param>
    /// <returns>True if at least one instance was killed</returns>
    public string KillApp(string appId)
    {
        var flatpakInstanceDtos = GetRunningInstances();

        var isRunning = flatpakInstanceDtos.Any(instance =>
            string.Equals(instance.AppId, appId, StringComparison.OrdinalIgnoreCase));


        if (flatpakInstanceDtos.Count == 0 || !isRunning)
        {
            return "Failed to find running instance of " + appId + ".";
        }

        var pid = flatpakInstanceDtos.Where(x => x.AppId == appId).Select(x => x.Pid).FirstOrDefault();

        if (pid <= 0) return "Failed to kill instance of " + appId + "." + pid;
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);

            process.Kill(true);
            return "Killed";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return "Failed to kill instance of " + appId + "." + pid;
    }

    /// <summary>
    /// Gets all currently running flatpak instances.
    /// </summary>
    /// <returns>List of running app IDs with their PIDs</returns>
    public List<FlatpakInstanceDto> GetRunningInstances()
    {
        var instances = new List<FlatpakInstanceDto>();

        var instancesPtr = FlatpakReference.InstanceGetAll();

        if (instancesPtr == IntPtr.Zero)
        {
            return instances;
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(instancesPtr);
            var length = Marshal.ReadInt32(instancesPtr + IntPtr.Size);

            for (var i = 0; i < length; i++)
            {
                var instancePtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (instancePtr == IntPtr.Zero) continue;

                if (FlatpakReference.InstanceIsActive(instancePtr))
                {
                    instances.Add(new FlatpakInstanceDto
                    {
                        AppId = PtrToStringSafe(FlatpakReference.InstanceGetApp(instancePtr)),
                        Pid = FlatpakReference.InstanceGetChildPid(instancePtr)
                    });
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(instancesPtr);
        }

        return instances;
    }

    /// <summary>
    /// Installs a flatpak package from a remote repository.
    /// </summary>
    /// <param name="appId">The application ID (e.g., "org.mozilla.firefox")</param>
    /// <param name="remoteName">The remote name (e.g., "flathub"). If null, will try the first available remote.</param>
    /// <returns>A result message indicating success or failure</returns>
    public string InstallApp(string appId, string? remoteName = null)
    {
        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return "Failed to get system installations.";
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            if (length == 0)
            {
                return "No flatpak installations found.";
            }

            var installationPtr = Marshal.ReadIntPtr(dataPtr);
            if (installationPtr == IntPtr.Zero)
            {
                return "Installation pointer is invalid.";
            }

            var remote = remoteName ?? GetFirstRemote(installationPtr);
            if (string.IsNullOrEmpty(remote))
            {
                return "No remote repository configured. Add a remote like 'flathub' first.";
            }

            var refString = $"app/{appId}/{GetCurrentArch()}/stable";

            var transactionPtr = FlatpakReference.TransactionNewForInstallation(
                installationPtr, IntPtr.Zero, out IntPtr transactionError);

            if (transactionError != IntPtr.Zero || transactionPtr == IntPtr.Zero)
            {
                return "Failed to create installation transaction.";
            }

            try
            {
                var addSuccess = FlatpakReference.TransactionAddInstall(
                    transactionPtr, remote, refString, IntPtr.Zero, out IntPtr addError);

                if (!addSuccess || addError != IntPtr.Zero)
                {
                    return $"Failed to add {appId} to installation queue. Check if the app ID is correct.";
                }

                var runSuccess = FlatpakReference.TransactionRun(
                    transactionPtr, IntPtr.Zero, out IntPtr runError);

                if (!runSuccess || runError != IntPtr.Zero)
                {
                    return $"Installation of {appId} failed. You may need elevated permissions.";
                }

                return $"Successfully installed {appId} from {remote}.";
            }
            finally
            {
                FlatpakReference.GObjectUnref(transactionPtr);
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }
    }

    /// <summary>
    /// Gets the first configured remote for an installation.
    /// </summary>
    private string? GetFirstRemote(IntPtr installationPtr)
    {
        IntPtr remotesPtr = FlatpakReference.InstallationListRemotes(
            installationPtr, IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || remotesPtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(remotesPtr);
            var length = Marshal.ReadInt32(remotesPtr + IntPtr.Size);

            if (length > 0)
            {
                IntPtr remotePtr = Marshal.ReadIntPtr(dataPtr);
                if (remotePtr != IntPtr.Zero)
                {
                    return PtrToStringSafe(FlatpakReference.RemoteGetName(remotePtr));
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(remotesPtr);
        }

        return null;
    }

    /// <summary>
    /// Uninstalls a flatpak application by its app ID or friendly name.
    /// </summary>
    /// <param name="nameOrId">The application ID (e.g., "org.mozilla.firefox") or friendly name</param>
    /// <returns>A result message indicating success or failure</returns>
    public string UninstallApp(string nameOrId)
    {
        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return "Failed to get system installations.";
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            var length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (var i = 0; i < length; i++)
            {
                var installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                var match = FindInstalledApp(installationPtr, nameOrId);

                if (match == null) continue;
                var refString = $"app/{match.Id}/{match.Arch}/{match.Branch}";

                var transactionPtr = FlatpakReference.TransactionNewForInstallation(
                    installationPtr, IntPtr.Zero, out IntPtr transactionError);

                if (transactionError != IntPtr.Zero || transactionPtr == IntPtr.Zero)
                {
                    return "Failed to create uninstallation transaction.";
                }

                try
                {
                    var addSuccess = FlatpakReference.TransactionAddUninstall(
                        transactionPtr, refString, out IntPtr addError);

                    if (!addSuccess || addError != IntPtr.Zero)
                    {
                        return $"Failed to add {nameOrId} to uninstallation queue.";
                    }

                    var runSuccess = FlatpakReference.TransactionRun(
                        transactionPtr, IntPtr.Zero, out IntPtr runError);

                    if (!runSuccess || runError != IntPtr.Zero)
                    {
                        return $"Uninstallation of {nameOrId} failed. You may need elevated permissions.";
                    }

                    return $"Successfully uninstalled {match.Name} ({match.Id}).";
                }
                finally
                {
                    FlatpakReference.GObjectUnref(transactionPtr);
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }

        return $"Could not find installed app matching '{nameOrId}'.";
    }

    /// <summary>
    /// Updates a flatpak application by its app ID or friendly name.
    /// </summary>
    /// <param name="nameOrId">The application ID (e.g., "org.mozilla.firefox") or friendly name</param>
    /// <returns>A result message indicating success or failure</returns>
    public string UpdateApp(string nameOrId)
    {
        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return "Failed to get system installations.";
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            var length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (var i = 0; i < length; i++)
            {
                var installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                var match = FindInstalledApp(installationPtr, nameOrId);

                if (match == null) continue;
                var refString = BuildRefString(match);

                var transactionPtr = FlatpakReference.TransactionNewForInstallation(
                    installationPtr, IntPtr.Zero, out IntPtr transactionError);

                if (transactionError != IntPtr.Zero || transactionPtr == IntPtr.Zero)
                {
                    return "Failed to create update transaction.";
                }

                try
                {
                    var addSuccess = FlatpakReference.TransactionAddUpdate(
                        transactionPtr, refString, IntPtr.Zero, null, out IntPtr addError);

                    if (!addSuccess || addError != IntPtr.Zero)
                    {
                        var response = FlatpakReference.GetErrorMessage(addError);
                        return
                            $"Failed to add {nameOrId} to update queue. Error: {response} App may already be up to date.";
                    }

                    var runSuccess = FlatpakReference.TransactionRun(
                        transactionPtr, IntPtr.Zero, out IntPtr runError);

                    if (!runSuccess || runError != IntPtr.Zero)
                    {
                        return $"Update of {nameOrId} failed. You may need elevated permissions.";
                    }

                    return $"Successfully updated {match.Name} ({match.Id}).";
                }
                finally
                {
                    FlatpakReference.GObjectUnref(transactionPtr);
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }

        return $"Could not find installed app matching '{nameOrId}'.";
    }

    /// <summary>
    /// Retrieve flatpak that require updates
    /// <returns>List of FlatpakPackageDto</returns>
    /// </summary>
    public List<FlatpakPackageDto> GetPackagesWithUpdates()
    {
        var packages = new List<FlatpakPackageDto>();

        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return packages;
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            var length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (var i = 0; i < length; i++)
            {
                var installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                var refsPtr = FlatpakReference.InstanceGetUpdates(
                    installationPtr, IntPtr.Zero, out IntPtr refsError);

                if (refsError != IntPtr.Zero || refsPtr == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var refsDataPtr = Marshal.ReadIntPtr(refsPtr);
                    var refsLength = Marshal.ReadInt32(refsPtr + IntPtr.Size);

                    for (var j = 0; j < refsLength; j++)
                    {
                        var refPtr = Marshal.ReadIntPtr(refsDataPtr + j * IntPtr.Size);
                        if (refPtr == IntPtr.Zero) continue;

                        var package = new FlatpackPackage(refPtr);
                        packages.Add(package.ToDto());
                    }
                }
                finally
                {
                    FlatpakReference.GPtrArrayUnref(refsPtr);
                }
            }
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }

        return packages;
    }

    /// <summary>
    /// Updates the local appstream metadata for a remote repository.
    /// </summary>
    /// <param name="remoteName">The remote name (e.g., "flathub"). If null, updates all remotes.</param>
    /// <param name="arch">The architecture (e.g., "x86_64"). If null, uses current system architecture.</param>
    /// <returns>Tuple with success boolean and result message</returns>
    public (bool success, string message) UpdateAppstream(string? remoteName = null, string? arch = null)
    {
        var installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return (false, "Failed to get system installations.");
        }

        try
        {
            var dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            if (length == 0)
            {
                return (false, "No flatpak installations found.");
            }

            var installationPtr = Marshal.ReadIntPtr(dataPtr);
            if (installationPtr == IntPtr.Zero)
            {
                return (false, "Installation pointer is invalid.");
            }

            var targetArch = arch ?? GetCurrentArch();
            var remote = remoteName ?? GetFirstRemote(installationPtr);

            if (string.IsNullOrEmpty(remote))
            {
                return (false, "No remote repository configured. Add a remote like 'flathub' first.");
            }

            var success = FlatpakReference.InstallationUpdateAppstreamSync(
                installationPtr,
                remote,
                targetArch,
                out bool outChanged,
                IntPtr.Zero,
                out IntPtr updateError);

            if (!success || updateError != IntPtr.Zero)
            {
                var errorMsg = FlatpakReference.GetErrorMessage(updateError);
                return (false, $"Failed to update appstream for {remote}: {errorMsg}");
            }

            var message = outChanged
                ? $"Successfully updated appstream for {remote}. Metadata was changed."
                : $"Appstream for {remote} is already up to date.";
            return (true, message);
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }
    }

    /// <summary>
    /// Gets all available apps from the local appstream metadata.
    /// </summary>
    /// <param name="remoteName">The remote name (e.g., "flathub"). I</param>
    /// <param name="arch">The architecture (e.g., "x86_64"). If null, uses current system architecture.</param>
    /// <returns>List of available applications from appstream</returns>
    public List<AppstreamApp> GetAvailableAppsFromAppstream(string remoteName, string? arch = null)
    {
        try
        {
            var targetArch = arch ?? GetCurrentArch();
            var remote = remoteName;

            if (string.IsNullOrEmpty(remote))
            {
                return new List<AppstreamApp>();
            }

            // Construct path to appstream file
            var appstreamPath = $"/var/lib/flatpak/appstream/{remote}/{targetArch}/active/appstream.xml";

            // Try .xml.gz if .xml doesn't exist
            if (!File.Exists(appstreamPath))
            {
                appstreamPath = $"/var/lib/flatpak/appstream/{remote}/{targetArch}/active/appstream.xml.gz";
            }

            if (!File.Exists(appstreamPath))
            {
                return new List<AppstreamApp>();
            }
            
            var parser = new AppstreamParser();
            return parser.ParseFile(appstreamPath);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to parse appstream: {e}");
        }
        return new List<AppstreamApp>();
    }
    
    /// <summary>
    /// Gets all available apps from appstream and serializes to JSON (AOT-compatible)
    /// </summary>
    /// <param name="remoteName">The remote name (e.g., "flathub"). If null, uses the first remote.</param>
    /// <param name="arch">The architecture (e.g., "x86_64"). If null, uses current system architecture.</param>
    /// <returns>JSON string of available applications</returns>
    public string GetAvailableAppsFromAppstreamJson(string? remoteName = null, string? arch = null)
    {
        var apps = GetAvailableAppsFromAppstream(remoteName, arch);
        return JsonSerializer.Serialize(apps, AppstreamJsonContext.Default.ListAppstreamApp);
    }

    /// <summary>
    /// Search Flathub return Api Response
    /// <param name="query">Query Parameter</param>
    /// <param name="page">Page of query</param>
    /// <param name="limit">Limit of each page</param>
    /// <param name="filters">Filters to apply on search</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>FlatpakApiResponse</returns>
    /// </summary>
    public async Task<FlatpakApiResponse> SearchFlathubAsync(
        string query,
        int page = 1,
        int limit = 21,
        List<FlatpakHttpRequests.FlathubSearchFilter>? filters = null,
        CancellationToken ct = default)
    {
        return await new FlatpakHttpRequests().SearchAsync(query, page, limit, filters, ct);
    }

    /// <summary>
    /// Search Flathub return json Response
    /// <param name="query">Query Parameter</param>
    /// <param name="page">Page of query</param>
    /// <param name="limit">Limit of each page</param>
    /// <param name="filters">Filters to apply on search</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>FlatpakApiResponse</returns>
    /// </summary>
    public async Task<string> SearchFlathubJsonAsync(
        string query,
        int page = 1,
        int limit = 21,
        List<FlatpakHttpRequests.FlathubSearchFilter>? filters = null,
        CancellationToken ct = default)
    {
        return await new FlatpakHttpRequests().SearchJsonAsync(query, page, limit, filters, ct);
    }


    /// <summary>
    /// Gets the current system architecture for flatpak refs.
    /// </summary>
    private static string GetCurrentArch()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "i386",
            Architecture.Arm => "arm",
            _ => "x86_64"
        };
    }

    /// <summary>
    /// Convert Ptr* to String
    /// </summary>
    private static string PtrToStringSafe(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    /// <summary>
    /// Builds a Flatpak ref string based on the package kind.
    /// </summary>
    private static string BuildRefString(FlatpakPackageDto package)
    {
        var kindString = package.Kind == FlatpakReference.FlatpakRefKindApp
            ? "app"
            : "runtime";

        return $"{kindString}/{package.Id}/{package.Arch}/{package.Branch}";
    }
}