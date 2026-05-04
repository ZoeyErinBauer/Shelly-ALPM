namespace Shelly.Gtk.Services;

public class PackageUpdateNotifier(IDirtyService dirtyService) : IPackageUpdateNotifier
{
    public event EventHandler? PackagesUpdated;

    public void NotifyPackagesUpdated()
    {
        PackagesUpdated?.Invoke(this, EventArgs.Empty);
        dirtyService.MarkDirty(DirtyScopes.NativeUpdates);
        dirtyService.MarkDirty(DirtyScopes.AurUpdates);
        dirtyService.MarkDirty(DirtyScopes.FlatpakUpdates);
    }
}
