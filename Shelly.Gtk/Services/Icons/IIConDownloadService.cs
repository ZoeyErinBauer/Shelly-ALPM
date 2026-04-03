namespace Shelly.Gtk.Services.Icons;

public interface IIConDownloadService
{
    public Task<bool> DownloadAndUnpackIcons();
}