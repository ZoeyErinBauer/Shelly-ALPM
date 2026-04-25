using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.AppImage;
using Shelly.Gtk.UiModels.PackageManagerObjects;


namespace Shelly.Gtk.Services;

public interface IUnprivilegedOperationService
{
    Task<OperationResult> RemoveFlatpakPackage(IEnumerable<string> packages);
    Task<List<FlatpakPackageDto>> ListFlatpakPackages();

    Task<List<FlatpakPackageDto>> ListFlatpakUpdates();

    Task<List<AppstreamApp>> ListAppstreamFlatpak(CancellationToken ct = default);

    Task<OperationResult> FlatpakUpgrade();

    Task<List<FlatpakRemoteDto>> FlatpakListRemotes();

    Task<OperationResult> UpdateFlatpakPackage(string package);

    Task<OperationResult> RemoveFlatpakPackage(string package, bool config);

    Task<OperationResult> InstallFlatpakPackage(string package, bool user,
        string remote, string branch, bool isRuntime = false);

    Task<OperationResult> FlatpakSyncRemoteAppstream();

    Task<OperationResult> FlatpakRemoveRemote(string remoteName, string scope);

    Task<OperationResult> FlatpakAddRemote(string remoteName, string scope, string url);
    
    Task<OperationResult> RunFlatpakName(string name);

    Task<OperationResult> FlatpakInsallFromRef(string path, string scope);

    Task<OperationResult> FlatpakInstallFromBundle(string path);

    Task<SyncModel> CheckForApplicationUpdates();

    Task<List<AlpmPackageUpdateDto>> CheckForStandardApplicationUpdates(bool showHidden = false);

    Task<OperationResult> ExportSyncFile(string filePath, string name);
    

    Task<ulong> GetFlatpakAppDataAsync(string remote, string app, string arch);
    
    Task<List<AppImageDto>> GetInstallAppImagesAsync();
    
    Task<List<AppImageDto>> GetUpdatesAppImagesAsync();
    
    Task<List<RssModel>> GetArchNewsAsync(bool all = false);
    
    Task<List<PacfileRecord>> GetPacFiles();
}