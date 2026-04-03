using System.Collections.Concurrent;
using System.Text.Json;

namespace Shelly.Gtk.Services.Icons;

public class IconResolverService : IIconResolverService
{
    private static readonly string[] Repos = ["archlinux-arch-extra", "archlinux-arch-core", "archlinux-arch-multilib"];
    private static readonly string[] Sizes = ["128x128", "64x64", "48x48"];

    private static readonly string[] ThemeSizes = ["128x128", "96x96", "64x64", "48x48", "32x32", "scalable"];
    private static readonly string[] IconExtensions = ["*.png", "*.svg"];

    private const string IconPath = "/usr/share/swcatalog";
    private const string LegacyIconPath = "/usr/share/app-info";
    private string _shellyStreamPath =  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/shelly-icons/shelly-icon-stream-main/");

    private readonly ConcurrentDictionary<string, string> _iconMap = [];
    private readonly Dictionary<string, List<string>> _shellyStreamManifest = [];

    private readonly IConfigService _configService;
    
    public IconResolverService(IConfigService configService)
    {
        _shellyStreamManifest = LoadShellyStreamManifest();
        _configService= configService;
    }

    //Commenting out extra methods and plan to add them back in after futher optimizations
    public string? GetIconPath(string packageName)
    {
        if (!_configService.LoadConfig().ShellyIconsEnabled)
        {
            return "Unavailable";
        }
        
        if (_iconMap.TryGetValue(packageName, out var iconPath))
        {
            return iconPath;
        }

        var shellyResult = GetShellyStreamIcon(packageName);
        if (shellyResult != null)
        {
            _iconMap.TryAdd(packageName, shellyResult);
            return shellyResult;
        }

        var swcatalogResult = GetSwcatalogIcon(packageName);
        _iconMap.TryAdd(packageName, swcatalogResult ?? "");
        return swcatalogResult ??
               null;
    }

    public void PreloadIcons(IEnumerable<string> packageNames)
    {
        foreach (var packageName in packageNames)
        {
            _ = GetIconPath(packageName);
        }
    }

    private string? GetSwcatalogIcon(string packageName)
    {
        var path = GetBasePath();
        if (string.IsNullOrEmpty(path))
            return null;

        return (from repo in Repos
                from size in Sizes
                select Path.Combine(path, "icons", repo, size)
                into dir
                where Directory.Exists(dir)
                select Directory.EnumerateFiles(dir, $"{packageName}_*.png").FirstOrDefault())
            .FirstOrDefault(x => x != null);
    }

    private string? GetBasePath() => Directory.Exists("/usr/share/swcatalog") ? IconPath :
        Directory.Exists(LegacyIconPath) ? LegacyIconPath : null;

    private Dictionary<string, List<string>> LoadShellyStreamManifest()
    {
        try
        {
            var manifestPath = Path.Combine(_shellyStreamPath, "manifest.json");
            if (!File.Exists(manifestPath))
                return [];

            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize(json, ShellyGtkJsonContext.Default.DictionaryStringListString) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private string? GetShellyStreamIcon(string packageName)
    {
        if (!_shellyStreamManifest.TryGetValue(packageName, out var icons) || icons.Count == 0)
            return null;
        
        foreach (var size in Sizes)
        {
            var icon = icons.FirstOrDefault(x => x.Contains(size));
            if (icon == null) continue;
            var fullPath = Path.Combine(_shellyStreamPath, icon);
            if (File.Exists(fullPath))
                return fullPath;
        }
        
        var firstIcon = icons.FirstOrDefault();
        if (firstIcon == null) return null;
        {
            var fullPath = Path.Combine(_shellyStreamPath, firstIcon);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}