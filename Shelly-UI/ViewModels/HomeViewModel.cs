using System.Reactive.Concurrency;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels;

public class HomeViewModel : ViewModelBase, IRoutableViewModel
{
    public HomeViewModel(IScreen screen)
    {
        HostScreen = screen;
        LoadData();
        LoadFeed();
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
        try
        {
            var feed = await GetRssFeedAsync("https://archlinux.org/feeds/news/");
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var item in feed)
                    FeedItems.Add(item);
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
}