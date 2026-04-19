using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;


public partial class ArchNewsService(IUnprivilegedOperationService unprivilegedOperationService) : IArchNewsService
{
    public async Task<List<RssModel>> FetchNewsAsync(CancellationToken ct)
    {
        try
        {
            var items = await unprivilegedOperationService.GetArchNewsAsync(true);
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
            return items;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to fetch Arch News: {e.Message}");
        }
        return [];
    }
}
