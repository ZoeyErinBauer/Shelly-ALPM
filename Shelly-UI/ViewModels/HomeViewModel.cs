using System.Reactive.Concurrency;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.Assets;
using Shelly_UI.Models;
using Shelly_UI.Models.PackageManagerObjects;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels;

public class HomeViewModel : ViewModelBase, IRoutableViewModel, IDisposable
{
    private readonly IPrivilegedOperationService _privilegedOperationService;

    private readonly IUnprivilegedOperationService _unprivilegedOperationService;

    public HomeViewModel(IScreen screen, IPrivilegedOperationService privilegedOperationService)
    {
        HostScreen = screen;
        _privilegedOperationService = privilegedOperationService;
        _unprivilegedOperationService = App.Services.GetService<IUnprivilegedOperationService>()!;

        SyncExport = ReactiveCommand.CreateFromTask(ExportSync);

        LoadData();
        LoadFeed();
    }

    private async void LoadData()
    {
        try
        {
            var packages = await _privilegedOperationService.GetInstalledPackagesAsync();
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                InstalledPackages = new ObservableCollection<AlpmPackageDto>(packages);
                this.RaisePropertyChanged(nameof(InstalledPackages));
            });
            var aur = await _privilegedOperationService.GetAurInstalledPackagesAsync();
            var flatpak = await _unprivilegedOperationService.ListFlatpakPackages();

            TotalPackages = packages.Count + aur.Count + flatpak.Count;

            var updates = await _unprivilegedOperationService.CheckForApplicationUpdates();

            var packagePercent =
                TotalPackages - (updates.Packages.Count + updates.Aur.Count + updates.Flatpaks.Count);

            var ratio = (double)packagePercent / TotalPackages * 100;
            
            GaugeLabel = $"{ratio:F2} %";
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load installed packages: {e.Message}");
        }
    }

    // Reference to IScreen that owns the routable view model.
    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ObservableCollection<AlpmPackageDto> InstalledPackages { get; set; } =
        [];

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
                Description = Regex.Replace(item.Element("description")?.Value ?? "", "<.*?>", string.Empty),
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

    private int _totalPackages = 0;

    public int TotalPackages
    {
        get => _totalPackages;
        set => this.RaiseAndSetIfChanged(ref _totalPackages, value);
    }

    private int _packagesForUpdates = 0;

    private string _gaugeLabel = "";

    public string GaugeLabel
    {
        get => _gaugeLabel;
        set => this.RaiseAndSetIfChanged(ref _gaugeLabel, value);
    }

    private async Task ExportSync()
    {
        MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

        try
        {
            var topLevel = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;
            if (topLevel == null) return;

            var suggestName = $"{DateTime.Now:yyyyMMddHHmmss}_shelly.sync";
            
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Resources.ExportSyncLocation,
                SuggestedFileName = $"{suggestName}", 
                DefaultExtension = "sync", 
                FileTypeChoices =
                [
                    new FilePickerFileType("Sync Files")
                    {
                        Patterns = ["*.sync"]
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });


            if (file != null)
            {
                var path = file.Path.LocalPath;
                path = path.Replace(file.Name, "");

                var result = await _unprivilegedOperationService.ExportSyncFile(path, file.Name.Equals(suggestName, StringComparison.InvariantCultureIgnoreCase) ? "" : file.Name.Replace(".sync", ""));
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to export sync file: {result.Error}");
                    mainWindow?.ShowToast(Resources.ExportSyncFailedToast + result.Error, isSuccess: false);
                }
                else
                {
                    mainWindow?.ShowToast(Resources.ExportSyncSuccessToast);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Unhandled exception in homepage sync export: {ex.Message}");
        }
    }

    public ReactiveCommand<Unit, Unit> SyncExport { get; }

    public void Dispose()
    {
        InstalledPackages?.Clear();
        FeedItems?.Clear();
    }
}