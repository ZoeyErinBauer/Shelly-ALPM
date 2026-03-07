using System.Threading.Tasks;

namespace Shelly.Gtk.Services;

public interface IUpdateService
{
    Task<bool> CheckForUpdateAsync();
    Task DownloadAndInstallUpdateAsync();
}
