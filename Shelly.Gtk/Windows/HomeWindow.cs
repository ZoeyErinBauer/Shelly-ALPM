using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows;

public class HomeWindow(
    IPrivilegedOperationService privilegedOperationService,
    IUnprivilegedOperationService unprivilegedOperationService,
    IConfigService configService,
    MetaSearch metaSearch) : IShellyWindow
{
    private Box _box = null!;
    private readonly CancellationTokenSource _cts = new();
    private ListBox? _listBox;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/HomeWindow.ui"), -1);
        _box = (Box)builder.GetObject("HomeWindow")!;

        var listBox = (ListBox)builder.GetObject("NewsListBox")!;
        _listBox = listBox;
        listBox.OnRealize += (sender, args) => { _ = LoadFeedAsync(listBox, _cts.Token); };

        var homeSearchEntry = (SearchEntry)builder.GetObject("HomeSearchEntry")!;
        var metaSearchContainer = (Box)builder.GetObject("MetaSearchContainer")!;
        var searchPromptOverlay = (Box)builder.GetObject("SearchPromptOverlay")!;

        homeSearchEntry.OnActivate += (_, _) =>
        {
            var query = homeSearchEntry.GetText();
            if (string.IsNullOrWhiteSpace(query)) return;
            
            searchPromptOverlay.SetVisible(false);
            
            while (metaSearchContainer.GetFirstChild() is { } child)
                metaSearchContainer.Remove(child);

            var metaSearchWidget = metaSearch.CreateWindow(query);
            metaSearchContainer.Append(metaSearchWidget);
            homeSearchEntry.SetText(string.Empty);
        };

        var totalAurLabel = (Label)builder.GetObject("TotalAurLabel")!;
        totalAurLabel.OnRealize += (sender, args) => { _ = LoadAurTotalData(totalAurLabel, _cts.Token); };

        var percentAurLabel = (Label)builder.GetObject("AurPercentLabel")!;
        percentAurLabel.OnRealize += (sender, args) => { _ = LoadAurPercentData(percentAurLabel, _cts.Token); };

        var totalPackageLabel = (Label)builder.GetObject("TotalPackagesLabel")!;
        totalPackageLabel.OnRealize += (sender, args) => { _ = LoadTotalPackageData(totalPackageLabel, _cts.Token); };

        var packagePercentLabel = (Label)builder.GetObject("StandardPercent")!;
        packagePercentLabel.OnRealize += (sender, args) =>
        {
            _ = LoadTotalPackagePercentData(packagePercentLabel, _cts.Token);
        };

        var totalFlatpakLabel = (Label)builder.GetObject("TotalFlatpakLabel")!;
        totalFlatpakLabel.OnRealize += (sender, args) => { _ = LoadTotalFlatpak(totalFlatpakLabel, _cts.Token); };

        var flatpakPercentLabel = (Label)builder.GetObject("FlatpakPercent")!;
        flatpakPercentLabel.OnRealize += (sender, args) => { _ = LoadPercentFlatpak(flatpakPercentLabel, _cts.Token); };

        var exportSyncButton = (Button)builder.GetObject("ExportSyncButton")!;
        exportSyncButton.OnClicked += (sender, args) => { _ = ExportSync(); };

        var config = configService.LoadConfig();
        var aurBox = (Box)builder.GetObject("AurBox")!;
        var flatpakBox = (Box)builder.GetObject("FlatpakBox")!;

        aurBox.Visible = config.AurEnabled;
        flatpakBox.Visible = config.FlatPackEnabled;

        configService.ConfigSaved += (sender, updatedConfig) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                aurBox.Visible = updatedConfig.AurEnabled;
                flatpakBox.Visible = updatedConfig.FlatPackEnabled;
                return false;
            });
        };

        return _box;
    }

    private async Task ExportSync()
    {
        try
        {
            var suggestName = $"{DateTime.Now:yyyyMMddHHmmss}_shelly.sync";

            var dialog = FileDialog.New();
            dialog.SetTitle("Export Sync File");
            dialog.SetInitialName(suggestName);

            var filter = FileFilter.New();
            filter.SetName("Sync Files (*.sync)");
            filter.AddPattern("*.sync");

            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(filter);
            dialog.SetFilters(filters);

            var file = await dialog.SaveAsync((Window)_box.GetRoot()!);

            if (file is not null)
            {
                var path = file.GetPath()!;

                // Generate whatever content you want to save
                var packages = await privilegedOperationService.GetInstalledPackagesAsync();
                var stringBuilder = new StringBuilder();
                foreach (var pkg in packages)
                {
                    stringBuilder.AppendLine(
                        $"{pkg.Name} - {pkg.Version} : Depends: {string.Join(",", pkg.Depends)} OptDepends {string.Join(",", pkg.OptDepends)}");
                }

                await System.IO.File.WriteAllTextAsync(path, stringBuilder.ToString());
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task LoadPercentFlatpak(Label label, CancellationToken ct)
    {
        var packages = await unprivilegedOperationService.ListFlatpakPackages();
        ct.ThrowIfCancellationRequested();
        var updates = await unprivilegedOperationService.CheckForApplicationUpdates();
        ct.ThrowIfCancellationRequested();
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                if (packages.Count == 0)
                {
                    label.SetText("N/A");
                    return false;
                }

                var ratio = (double)(packages.Count - updates.Flatpaks.Count) / packages.Count * 100;
                var labelText = $"{ratio:F2} %";
                label.SetText(labelText);
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task LoadTotalFlatpak(Label label, CancellationToken ct)
    {
        var packages = await unprivilegedOperationService.ListFlatpakPackages();
        ct.ThrowIfCancellationRequested();
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                label.SetText(packages.Count.ToString());
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task LoadTotalPackagePercentData(Label label, CancellationToken ct)
    {
        var packages = await privilegedOperationService.GetInstalledPackagesAsync();
        ct.ThrowIfCancellationRequested();
        var updates = await unprivilegedOperationService.CheckForApplicationUpdates();
        ct.ThrowIfCancellationRequested();
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                if (packages.Count == 0)
                {
                    label.SetText("N/A");
                    return false;
                }

                var ratio = (double)(packages.Count - updates.Packages.Count) / packages.Count * 100;
                var labelText = $"{ratio:F2} %";
                label.SetText(labelText);
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task LoadTotalPackageData(Label label, CancellationToken ct)
    {
        var packages = await privilegedOperationService.GetInstalledPackagesAsync();
        ct.ThrowIfCancellationRequested();
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateTotalPackageLabel(label, packages);
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void PopulateTotalPackageLabel(Label label, List<AlpmPackageDto> packages)
    {
        label.SetText(packages.Count.ToString());
    }

    private async Task LoadAurPercentData(Label label, CancellationToken ct)
    {
        var aurPackages = await privilegedOperationService.GetAurInstalledPackagesAsync();
        ct.ThrowIfCancellationRequested();
        var updates = await unprivilegedOperationService.CheckForApplicationUpdates();
        ct.ThrowIfCancellationRequested();
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateAurPercentLabel(label, aurPackages, updates);
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void PopulateAurPercentLabel(Label label, List<AurPackageDto> packages, SyncModel syncModel)
    {
        if (packages.Count == 0)
        {
            label.SetText("N/A");
            return;
        }

        var ratio = (double)(packages.Count - syncModel.Aur.Count) / packages.Count * 100;
        var labelText = $"{ratio:F2} %";
        label.SetText(labelText);
    }

    private async Task LoadAurTotalData(Label label, CancellationToken ct)
    {
        var aurPackages = await privilegedOperationService.GetAurInstalledPackagesAsync();
        ct.ThrowIfCancellationRequested();
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateAurTotalLabel(label, aurPackages);
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void PopulateAurTotalLabel(Label label, List<AurPackageDto> packages)
    {
        label.SetText(packages.Count.ToString());
    }


    private static async Task LoadFeedAsync(ListBox listBox, CancellationToken ct)
    {
        var feedItems = new List<RssModel>();

        // Fetch from network
        try
        {
            var feed = await GetRssFeedAsync("https://archlinux.org/feeds/news/", ct);
            ct.ThrowIfCancellationRequested();
            feedItems.AddRange(feed);

            // Marshal back to GTK main thread to update UI
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateListBox(listBox, feedItems);
                return false; // run once
            });

            // Cache the result
            var cachedFeed = new CachedRssModel
            {
                TimeCached = DateTime.Now,
                Rss = feedItems
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void PopulateListBox(ListBox listBox, List<RssModel> items)
    {
        // Clear existing rows
        while (listBox.GetFirstChild() is { } child)
            listBox.Remove(child);

        foreach (var item in items)
        {
            var row = new ListBoxRow();
            var vbox = Box.New(Orientation.Vertical, 4);
            vbox.MarginStart = 8;
            vbox.MarginEnd = 8;
            vbox.MarginTop = 4;
            vbox.MarginBottom = 4;

            var title = Label.New(item.Title);
            title.Halign = Align.Start;
            title.Wrap = true;
            title.AddCssClass("heading");

            var date = Label.New(item.PubDate);
            date.Halign = Align.Start;
            date.AddCssClass("dim-label");

            var desc = Label.New(item.Description);
            desc.Halign = Align.Start;
            desc.Wrap = true;

            vbox.Append(title);
            vbox.Append(date);
            vbox.Append(desc);

            row.SetChild(vbox);
            listBox.Append(row);
        }
    }

    // Port these from HomeViewModel or reference them from a shared service
    private static async Task<List<RssModel>> GetRssFeedAsync(string url, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        var xmlString = await client.GetStringAsync(url, ct);
        var xml = XDocument.Parse(xmlString);

        return xml.Descendants("item").Select(item => new RssModel
        {
            Title = item.Element("title")?.Value ?? "", Link = item.Element("link")?.Value ?? "",
            Description = Regex.Replace(item.Element("description")?.Value ?? "", "<.*?>", string.Empty),
            PubDate = item.Element("pubDate")?.Value ?? ""
        }).ToList();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}