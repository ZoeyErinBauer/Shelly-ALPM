using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;

namespace Shelly_UI.ViewModels;

public class HomeViewModel : ViewModelBase, IRoutableViewModel
{
    public HomeViewModel(IScreen screen)
    {
        HostScreen = screen;
        InstalledPackages = new ObservableCollection<AlpmPackage>(new AlpmManager().GetInstalledPackages());
        LoadFeed();
    }

    // Reference to IScreen that owns the routable view model.
    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ObservableCollection<AlpmPackage> InstalledPackages { get; set; }
    
    public ObservableCollection<RssModel> FeedItems { get; } = new ObservableCollection<RssModel>();

    public HomeViewModel()
    {
        LoadFeed();
    }

    private async void LoadFeed()
    {
        try
        {
            var feed = await GetRssFeedAsync("https://archlinux.org/feeds/news/");
            foreach (var item in feed)
                FeedItems.Add(item);
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