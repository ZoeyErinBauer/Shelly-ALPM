using System.Reactive.Concurrency;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class HomeViewModel : ViewModelBase, IRoutableViewModel
{
    private IAppCache _appCache;
    
    public HomeViewModel(IScreen screen, IAppCache appCache)
    {
        HostScreen = screen;
        LoadData();
        LoadFeed();
        _appCache = appCache;
    }

    private async void LoadData()
    {
        try
        {
            var packages = await Task.Run(() => AlpmService.Instance.GetInstalledPackages());
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                InstalledPackages = new ObservableCollection<AlpmPackageDto>(packages);
                this.RaisePropertyChanged(nameof(InstalledPackages));
                _appCache.StoreAsync(nameof(CacheEnums.InstalledCache), packages);
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load installed packages: {e.Message}");
        }
    }

    // Reference to IScreen that owns the routable view model.
    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ObservableCollection<AlpmPackageDto> InstalledPackages { get; set; }
    
    public ObservableCollection<RssModel> FeedItems { get; } = new ObservableCollection<RssModel>();
    

    private async void LoadFeed()
    {
        //Try from cache or time expired
        try
        {
            var rssFeed = LoadCachedFeed();
            if (rssFeed.TimeCached.HasValue &&
                DateTime.Now.Subtract(rssFeed.TimeCached.Value).TotalMinutes < 15)
            {
                foreach (var item in rssFeed.Rss) FeedItems.Add(item);
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        //load from feed
        try
        {
            var feed = await GetRssFeedAsync("https://archlinux.org/feeds/news/");
            var cachedFeed = new CachedRssModel();
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var item in feed)
                {
                    FeedItems.Add(item);
                    cachedFeed.Rss.Add(item);
                }
                cachedFeed.TimeCached = DateTime.Now;
                CacheFeed(cachedFeed);
            });
           
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<ObservableCollection<RssModel>> GetRssFeedAsync(string url)
    {
        var items = new ObservableCollection<RssModel>();

        using var client = new HttpClient();
        var xmlString = await client.GetStringAsync(url);

        var xml = XDocument.Parse(xmlString);

        // Standard RSS feed uses <item> nodes
        foreach (var item in xml.Descendants("item"))
        {
            items.Add(new RssModel
            {
                Title = item.Element("title")?.Value ?? "",
                Link = item.Element("link")?.Value ?? "",
                Description =  Regex.Replace(item.Element("description")?.Value ?? "" , "<.*?>", string.Empty),
                PubDate = item.Element("pubDate")?.Value ?? ""
            });
        }
        return items;
    }
    
    #region RssCaching
    
    private static readonly string FeedFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "Shelly");
    
    private static readonly string FeedPath = Path.Combine(FeedFolder, "Feed.json");

    public static void CacheFeed(CachedRssModel feed)
    {
        if (!Directory.Exists(FeedFolder)) Directory.CreateDirectory(FeedFolder);
        
        var json = JsonSerializer.Serialize(feed, ShellyUIJsonContext.Default.CachedRssModel);
        File.WriteAllText(FeedPath, json);
    }

    public static CachedRssModel LoadCachedFeed()
    {
        if (!File.Exists(FeedPath)) return new CachedRssModel(); 

        try
        {
            var json = File.ReadAllText(FeedPath);
            return JsonSerializer.Deserialize(json, ShellyUIJsonContext.Default.CachedRssModel) ?? new CachedRssModel();
        }
        catch
        {
            return new CachedRssModel();
        }
    }
    #endregion
}