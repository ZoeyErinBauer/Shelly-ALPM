namespace Shelly.Gtk.Services;

public class PackageUpdateNotifier : IPackageUpdateNotifier
{
    public event EventHandler? PackagesUpdated;

    public void NotifyPackagesUpdated()
    {
        PackagesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
