using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;


public partial class ArchNewsService(IUnprivilegedOperationService unprivilegedOperationService, IDirtyService dirtyService) : IArchNewsService
{
    public async Task<List<RssModel>> FetchNewsAsync(CancellationToken ct)
    {
        try
        {
            var items = await unprivilegedOperationService.GetArchNewsAsync(true);
            dirtyService.MarkDirty(DirtyScopes.News);
            return items;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to fetch Arch News: {e.Message}");
        }
        return [];
    }
    
    public async Task<List<RssModel>> FindNewNewsAsync(CancellationToken ct)
    {
        try
        {
            var items = await unprivilegedOperationService.GetArchNewsAsync();
            dirtyService.MarkDirty(DirtyScopes.News);
            return items;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to fetch Arch News: {e.Message}");
        }
        return [];
    }
}
