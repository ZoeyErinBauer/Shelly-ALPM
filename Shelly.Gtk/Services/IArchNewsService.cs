using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;

public interface IArchNewsService
{
    Task<List<RssModel>> FetchNewsAsync(CancellationToken ct);
    
    Task<List<RssModel>> FindNewNewsAsync(CancellationToken ct);
}