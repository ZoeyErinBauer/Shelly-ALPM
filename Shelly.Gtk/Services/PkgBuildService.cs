using Gtk;
using Shelly.Gtk.Windows.Dialog;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;

public class PkgBuildService : IPkgBuildService
{
    private readonly HttpClient _httpClient = new();

    public async Task ShowPreviewAsync(Overlay parentOverlay, string packageName)
    {
        try
        {
            string url = $"https://aur.archlinux.org/cgit/aur.git/plain/PKGBUILD?h={packageName}";
            string content = await _httpClient.GetStringAsync(url);
            
            GLib.Functions.IdleAdd(0, () => 
            {
                var args = new PackageBuildEventArgs($"PKGBUILD: {packageName}", content);
            
                PkgbuildPreview.ShowPackageBuildPreview(parentOverlay, args);
            
                return false; 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no serviço: {ex.Message}");
        }
    }
}