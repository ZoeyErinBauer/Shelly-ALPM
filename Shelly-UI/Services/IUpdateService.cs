using System.Threading.Tasks;

namespace Shelly_UI.Services;

public interface IUpdateService
{
    Task<bool> CheckForUpdateAsync();
    Task DownloadAndInstallUpdateAsync();
}
