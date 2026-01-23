using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PackageManager.Flatpak;

public class FlatpakManager
{
    public List<FlatpakPackageDto> SearchInstalled()
    {
        var packages = new List<FlatpakPackageDto>();

        IntPtr installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return packages;
        }

        try
        {
            // GPtrArray structure: first field is data pointer, second is length
            IntPtr dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (int i = 0; i < length; i++)
            {
                IntPtr installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                // Get installed refs for this installation
                IntPtr refsPtr = FlatpakReference.InstallationListInstalledRefs(
                    installationPtr, IntPtr.Zero, out IntPtr refsError);

                if (refsError != IntPtr.Zero || refsPtr == IntPtr.Zero)
                {
                    FlatpakReference.GObjectUnref(installationPtr);
                    continue;
                }

                try
                {
                    IntPtr refsDataPtr = Marshal.ReadIntPtr(refsPtr);
                    int refsLength = Marshal.ReadInt32(refsPtr + IntPtr.Size);

                    for (int j = 0; j < refsLength; j++)
                    {
                        IntPtr refPtr = Marshal.ReadIntPtr(refsDataPtr + j * IntPtr.Size);
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
        IntPtr installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (int i = 0; i < length; i++)
            {
                IntPtr installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                // Find the matching package to get its details
                var match = FindInstalledApp(installationPtr, nameOrId);

                if (match != null)
                {
                    bool success = FlatpakReference.InstallationLaunch(
                        installationPtr,
                        match.Id,
                        match.Arch,
                        match.Branch,
                        null, // commit
                        IntPtr.Zero, // cancellable
                        out IntPtr launchError);

                    if (success && launchError == IntPtr.Zero)
                    {
                        return true;
                    }
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
        IntPtr refsPtr = FlatpakReference.InstallationListInstalledRefs(
            installationPtr, IntPtr.Zero, out IntPtr refsError);

        if (refsError != IntPtr.Zero || refsPtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            IntPtr refsDataPtr = Marshal.ReadIntPtr(refsPtr);
            int refsLength = Marshal.ReadInt32(refsPtr + IntPtr.Size);

            for (int j = 0; j < refsLength; j++)
            {
                IntPtr refPtr = Marshal.ReadIntPtr(refsDataPtr + j * IntPtr.Size);
                if (refPtr == IntPtr.Zero) continue;

                var package = new FlatpackPackage(refPtr);

                // Match by ID (exact or contains) or by friendly name
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

        bool isRunning = flatpakInstanceDtos.Any(instance => 
            string.Equals(instance.AppId, appId, StringComparison.OrdinalIgnoreCase));
        
        
        if (flatpakInstanceDtos.Count == 0 || !isRunning)
        {
            return "Failed to find running instance of " + appId + ".";
        }
        
        int pid = flatpakInstanceDtos.Where(x => x.AppId == appId).Select(x => x.Pid).FirstOrDefault();
        
        if (pid > 0)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                
                process.Kill(true); 
                return "Killed";
            }
            catch
            {
                // Process may have already exited
            }
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

        IntPtr instancesPtr = FlatpakReference.InstanceGetAll();

        if (instancesPtr == IntPtr.Zero)
        {
            return instances;
        }

        try
        {
            IntPtr dataPtr = Marshal.ReadIntPtr(instancesPtr);
            int length = Marshal.ReadInt32(instancesPtr + IntPtr.Size);

            for (int i = 0; i < length; i++)
            {
                IntPtr instancePtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
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
        IntPtr installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return "Failed to get system installations.";
        }

        try
        {
            IntPtr dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            if (length == 0)
            {
                return "No flatpak installations found.";
            }

            // Use the first installation (typically the system installation)
            IntPtr installationPtr = Marshal.ReadIntPtr(dataPtr);
            if (installationPtr == IntPtr.Zero)
            {
                return "Installation pointer is invalid.";
            }

            // If no remote specified, get the first available remote
            string remote = remoteName ?? GetFirstRemote(installationPtr);
            if (string.IsNullOrEmpty(remote))
            {
                return "No remote repository configured. Add a remote like 'flathub' first.";
            }

            // Build the ref string (format: app/org.example.App/x86_64/stable)
            string refString = $"app/{appId}/{GetCurrentArch()}/stable";

            // Create a transaction for the installation
            IntPtr transactionPtr = FlatpakReference.TransactionNewForInstallation(
                installationPtr, IntPtr.Zero, out IntPtr transactionError);

            if (transactionError != IntPtr.Zero || transactionPtr == IntPtr.Zero)
            {
                return "Failed to create installation transaction.";
            }

            try
            {
                // Add the install operation to the transaction
                bool addSuccess = FlatpakReference.TransactionAddInstall(
                    transactionPtr, remote, refString, IntPtr.Zero, out IntPtr addError);

                if (!addSuccess || addError != IntPtr.Zero)
                {
                    return $"Failed to add {appId} to installation queue. Check if the app ID is correct.";
                }

                // Run the transaction
                bool runSuccess = FlatpakReference.TransactionRun(
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
            IntPtr dataPtr = Marshal.ReadIntPtr(remotesPtr);
            int length = Marshal.ReadInt32(remotesPtr + IntPtr.Size);

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
        IntPtr installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return "Failed to get system installations.";
        }

        try
        {
            IntPtr dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (int i = 0; i < length; i++)
            {
                IntPtr installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                // Find the installed app to get its full ref details
                var match = FindInstalledApp(installationPtr, nameOrId);

                if (match != null)
                {
                    // Build the ref string (format: app/org.example.App/x86_64/stable)
                    string refString = $"app/{match.Id}/{match.Arch}/{match.Branch}";

                    // Create a transaction for the uninstallation
                    IntPtr transactionPtr = FlatpakReference.TransactionNewForInstallation(
                        installationPtr, IntPtr.Zero, out IntPtr transactionError);

                    if (transactionError != IntPtr.Zero || transactionPtr == IntPtr.Zero)
                    {
                        return "Failed to create uninstallation transaction.";
                    }

                    try
                    {
                        // Add the uninstall operation to the transaction
                        bool addSuccess = FlatpakReference.TransactionAddUninstall(
                            transactionPtr, refString, out IntPtr addError);

                        if (!addSuccess || addError != IntPtr.Zero)
                        {
                            return $"Failed to add {nameOrId} to uninstallation queue.";
                        }

                        // Run the transaction
                        bool runSuccess = FlatpakReference.TransactionRun(
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
        IntPtr installationsPtr = FlatpakReference.GetSystemInstallations(IntPtr.Zero, out IntPtr error);

        if (error != IntPtr.Zero || installationsPtr == IntPtr.Zero)
        {
            return "Failed to get system installations.";
        }

        try
        {
            IntPtr dataPtr = Marshal.ReadIntPtr(installationsPtr);
            int length = Marshal.ReadInt32(installationsPtr + IntPtr.Size);

            for (int i = 0; i < length; i++)
            {
                IntPtr installationPtr = Marshal.ReadIntPtr(dataPtr + i * IntPtr.Size);
                if (installationPtr == IntPtr.Zero) continue;

                // Find the installed app to get its full ref details
                var match = FindInstalledApp(installationPtr, nameOrId);

                if (match != null)
                {
                    // Build the ref string (format: app/org.example.App/x86_64/stable)
                    string refString = $"app/{match.Id}/{match.Arch}/{match.Branch}";

                    // Create a transaction for the update
                    IntPtr transactionPtr = FlatpakReference.TransactionNewForInstallation(
                        installationPtr, IntPtr.Zero, out IntPtr transactionError);

                    if (transactionError != IntPtr.Zero || transactionPtr == IntPtr.Zero)
                    {
                        return "Failed to create update transaction.";
                    }

                    try
                    {
                        // Add the update operation to the transaction
                        bool addSuccess = FlatpakReference.TransactionAddUpdate(
                            transactionPtr, refString, IntPtr.Zero, null, out IntPtr addError);

                        if (!addSuccess || addError != IntPtr.Zero)
                        {
                            return $"Failed to add {nameOrId} to update queue. App may already be up to date.";
                        }

                        // Run the transaction
                        bool runSuccess = FlatpakReference.TransactionRun(
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
        }
        finally
        {
            FlatpakReference.GPtrArrayUnref(installationsPtr);
        }

        return $"Could not find installed app matching '{nameOrId}'.";
    }

    public async Task<FlatpakApiResponse> SearchFlathubAsync(
        string query,
        int page = 1,
        int limit = 21,
        List<FlatpakHttpRequests.FlathubSearchFilter>? filters = null,
        CancellationToken ct = default)
    { 
        return await new FlatpakHttpRequests().SearchAsync(query, page, limit, filters, ct);;
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

    private static string PtrToStringSafe(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }
}

