namespace Shelly.Gtk.Services;

public interface IPackageUpdateNotifier
{
    event EventHandler? PackagesUpdated;
    void NotifyPackagesUpdated();
}
