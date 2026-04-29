using System.Diagnostics.Tracing;
using System.Text;
using Gtk;
using Microsoft.VisualBasic.FileIO;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.Services.Icons;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;
using Shelly.Gtk.Windows.Dialog;

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows;

public class HomeWindow(
    IPrivilegedOperationService privilegedOperationService,
    IUnprivilegedOperationService unprivilegedOperationService,
    IConfigService configService,
    ILockoutService lockoutService,
    IIconResolverService iconResolverService,
    IGenericQuestionService genericQuestionService,
    IArchNewsService archNewsService,
    IOperationLogService operationLogService,
    IIConDownloadService iconDownloadService,
    MetaSearch metaSearch) : IShellyWindow
{
    private Box _box = null!;
    private Box? _logBox;
    private readonly CancellationTokenSource _cts = new();
    private ListBox? _updatesListBox;
    private List<RssModel> _archNewsItems = [];
    private List<RssModel> _newArchNewsItems = [];
    private List<OperationLogEntry>? _logEntries = [];
    private List<string> _logLines = [];
    private Label? _totalAurLabel;
    private Label? _percentAurLabel;
    private Label? _totalPackageLabel;
    private Label? _packagePercentLabel;
    private Label? _totalFlatpakLabel;
    private Label? _flatpakPercentLabel;
    private ListBox? _operationLogListBox;
    private Button _archNewsButton = null!;
    private Widget? _activeSessionLogOverlay;
    private Overlay _overlay = null!;
    private GenericDialogEventArgs _args;
    private Label? _availableUpdatesLabel;
    private uint _updateTimerId;
    private const int MaxRawLineBytes = 50 * 1024 * 1024; // 50 MB
    private GObject.SignalHandler<ListBox, ListBox.RowActivatedSignalArgs>? _logRowActivatedHandler;
    GObject.SignalHandler<Adjustment>? _vadjHandler;
    GObject.SignalHandler<Adjustment>? _clickHandler;


    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/HomeWindow.ui"), -1);
        _overlay = (Overlay)builder.GetObject("HomeWindowOverlay")!;
        _box = (Box)builder.GetObject("HomeWindow")!;

        _updatesListBox = (ListBox)builder.GetObject("UpdatesListBox")!;
        _archNewsButton = (Button)builder.GetObject("ArchNewsButton")!;

        var homeSearchEntry = (SearchEntry)builder.GetObject("HomeSearchEntry")!;
        var metaSearchContainer = (Box)builder.GetObject("MetaSearchContainer")!;
        var searchPromptOverlay = (Box)builder.GetObject("SearchPromptOverlay")!;

        _availableUpdatesLabel = (Label)builder.GetObject("AvailableUpdateLabel")!;
        if (configService.LoadConfig().ShellyIconsEnabled)
        {
            Task.Run(async () =>
            {
                await iconDownloadService.DownloadAndUnpackIcons();
                return Task.CompletedTask;
            });
        }

        homeSearchEntry.OnActivate += (_, _) =>
        {
            var query = homeSearchEntry.GetText();
            if (string.IsNullOrWhiteSpace(query)) return;

            searchPromptOverlay.SetVisible(false);

            while (metaSearchContainer.GetFirstChild() is { } child)
                metaSearchContainer.Remove(child);

            var metaSearchWidget = metaSearch.CreateWindow(query);
            metaSearchContainer.Append(metaSearchWidget);
        };

        _totalAurLabel = (Label)builder.GetObject("TotalAurLabel")!;
        _totalAurLabel.OnRealize += (sender, args) => { _ = LoadAurTotalData(_totalAurLabel, _cts.Token); };

        _percentAurLabel = (Label)builder.GetObject("AurPercentLabel")!;
        _percentAurLabel.OnRealize += (sender, args) => { _ = LoadAurPercentData(_percentAurLabel, _cts.Token); };

        _totalPackageLabel = (Label)builder.GetObject("TotalPackagesLabel")!;
        _totalPackageLabel.OnRealize += (sender, args) => { _ = LoadTotalPackageData(_totalPackageLabel, _cts.Token); };

        _packagePercentLabel = (Label)builder.GetObject("StandardPercent")!;
        _packagePercentLabel.OnRealize += (sender, args) =>
        {
            _ = LoadTotalPackagePercentData(_packagePercentLabel, _cts.Token);
        };

        _totalFlatpakLabel = (Label)builder.GetObject("TotalFlatpakLabel")!;
        _totalFlatpakLabel.OnRealize += (sender, args) => { _ = LoadTotalFlatpak(_totalFlatpakLabel, _cts.Token); };

        _flatpakPercentLabel = (Label)builder.GetObject("FlatpakPercent")!;
        _flatpakPercentLabel.OnRealize += (sender, args) =>
        {
            _ = LoadPercentFlatpak(_flatpakPercentLabel, _cts.Token);
        };

        var exportSyncButton = (Button)builder.GetObject("ExportSyncButton")!;
        exportSyncButton.OnClicked += (sender, args) => { _ = ExportSync(); };

        var upgradeAllButton = (Button)builder.GetObject("UpgradeAllButton")!;
        upgradeAllButton.OnClicked += (sender, args) => { _ = UpgradeAll(); };

        var cacheCleanButton = (Button)builder.GetObject("CacheCleanButton")!;
        cacheCleanButton.OnClicked += (sender, args) => OpenCacheCleanDialog();

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

        _ = FindNewNews(_cts.Token);

        _operationLogListBox = (ListBox)builder.GetObject("OperationLogListBox")!;
        _operationLogListBox.OnRealize += (sender, args) => { _ = LoadOperationLog(_cts.Token); };

        _archNewsButton.OnClicked += (_, _) => OpenArchNewsOverlay();

        _ = LoadUpdatesPanel(_updatesListBox!, _cts.Token);
        _updateTimerId = GLib.Functions.TimeoutAdd(200, 180000, () =>
        {
            _ = LoadUpdatesPanel(_updatesListBox!, _cts.Token);
            return true;
        });
        return _overlay;
    }

    private async void OpenArchNewsOverlay()
    {
        if (_archNewsItems.Count == 0)
        {
            await LoadArchNews(_cts.Token);
        }

        var container = new Box();
        container.SetOrientation(Orientation.Vertical);
        container.SetSpacing(10);
        container.SetMarginBottom(10);
        container.SetMarginEnd(10);
        container.SetMarginStart(10);
        container.SetMarginTop(10);

        var titleLabel = Label.New("Arch Linux News");
        titleLabel.AddCssClass("title-1");
        titleLabel.Xalign = 0;
        container.Append(titleLabel);

        var listBox = new ListBox();
        listBox.SetSelectionMode(SelectionMode.None);
        listBox.AddCssClass("rich-list");

        var scrolledWindow = new ScrolledWindow();
        scrolledWindow.SetVexpand(true);
        scrolledWindow.HscrollbarPolicy = PolicyType.Never;
        scrolledWindow.SetChild(listBox);
        container.Append(scrolledWindow);

        var args = new GenericDialogEventArgs(container);
        GenericOverlay.ShowGenericOverlay(_overlay, container, args, 700, 500);

        if (_archNewsItems.Count == 0)
        {
            var placeholder = Label.New("No news available");
            placeholder.AddCssClass("dim-label");
            placeholder.Halign = Align.Center;
            placeholder.MarginTop = 20;
            listBox.Append(placeholder);
        }
        else
        {
            foreach (var item in _archNewsItems)
            {
                var row = new ListBoxRow();
                var vbox = Box.New(Orientation.Vertical, 5);
                vbox.MarginStart = 10;
                vbox.MarginEnd = 10;
                vbox.MarginTop = 10;
                vbox.MarginBottom = 10;

                var newsTitle = Label.New(item.Title);
                newsTitle.AddCssClass("title-4");
                newsTitle.Xalign = 0;
                newsTitle.Wrap = true;
                vbox.Append(newsTitle);

                if (!string.IsNullOrEmpty(item.PubDate))
                {
                    var dateLabel = Label.New(item.PubDate);
                    dateLabel.AddCssClass("caption");
                    dateLabel.AddCssClass("dim-label");
                    dateLabel.Xalign = 0;
                    vbox.Append(dateLabel);
                }

                if (!string.IsNullOrEmpty(item.Description))
                {
                    var descLabel = Label.New(item.Description);
                    descLabel.Xalign = 0;
                    descLabel.Wrap = true;
                    descLabel.Lines = 3;
                    descLabel.Ellipsize = Pango.EllipsizeMode.End;
                    vbox.Append(descLabel);
                }

                row.SetChild(vbox);
                listBox.Append(row);
            }
        }
    }

    private async void ShowNewNews()
    {
        var container = new Box();
        container.SetOrientation(Orientation.Vertical);
        container.SetSpacing(10);
        container.SetMarginBottom(10);
        container.SetMarginEnd(10);
        container.SetMarginStart(10);
        container.SetMarginTop(10);

        var titleLabel = Label.New("New Arch Linux News");
        titleLabel.AddCssClass("title-1");
        titleLabel.Xalign = 0;
        container.Append(titleLabel);

        var listBox = new ListBox();
        listBox.SetSelectionMode(SelectionMode.None);
        listBox.AddCssClass("rich-list");

        var scrolledWindow = new ScrolledWindow();
        scrolledWindow.SetVexpand(true);
        scrolledWindow.HscrollbarPolicy = PolicyType.Never;
        scrolledWindow.SetChild(listBox);
        container.Append(scrolledWindow);

        var args = new GenericDialogEventArgs(container);
        GenericOverlay.ShowGenericOverlay(_overlay, container, args, 700, 500);

        if (_newArchNewsItems.Count == 0)
        {
            var placeholder = Label.New("No news available");
            placeholder.AddCssClass("dim-label");
            placeholder.Halign = Align.Center;
            placeholder.MarginTop = 20;
            listBox.Append(placeholder);
        }
        else
        {
            foreach (var item in _newArchNewsItems)
            {
                var row = new ListBoxRow();
                var vbox = Box.New(Orientation.Vertical, 5);
                vbox.MarginStart = 10;
                vbox.MarginEnd = 10;
                vbox.MarginTop = 10;
                vbox.MarginBottom = 10;

                var newsTitle = Label.New(item.Title);
                newsTitle.AddCssClass("title-4");
                newsTitle.Xalign = 0;
                newsTitle.Wrap = true;
                vbox.Append(newsTitle);

                if (!string.IsNullOrEmpty(item.PubDate))
                {
                    var dateLabel = Label.New(item.PubDate);
                    dateLabel.AddCssClass("caption");
                    dateLabel.AddCssClass("dim-label");
                    dateLabel.Xalign = 0;
                    vbox.Append(dateLabel);
                }

                if (!string.IsNullOrEmpty(item.Description))
                {
                    var descLabel = Label.New(item.Description);
                    descLabel.Xalign = 0;
                    descLabel.Wrap = true;
                    descLabel.Lines = 3;
                    descLabel.Ellipsize = Pango.EllipsizeMode.End;
                    vbox.Append(descLabel);
                }

                row.SetChild(vbox);
                listBox.Append(row);
            }
        }
    }

    private async Task UpgradeAll()
    {
        try
        {
            var packagesNeedingUpdate = await unprivilegedOperationService.CheckForApplicationUpdates();

            if (packagesNeedingUpdate.Aur.Count == 0 && packagesNeedingUpdate.Packages.Count == 0 &&
                packagesNeedingUpdate.Flatpaks.Count == 0)
            {
                var toastArgs = new ToastMessageEventArgs("No packages need to be upgraded");
                genericQuestionService.RaiseToastMessage(toastArgs);
                return;
            }

            var standardPackagesNeedingUpdate = packagesNeedingUpdate.Packages;
            if (standardPackagesNeedingUpdate.Count == 0)
            {
                var toastArgs = new ToastMessageEventArgs("Standard Packages is already up to date");
                genericQuestionService.RaiseToastMessage(toastArgs);
            }

            if (!configService.LoadConfig().NoConfirm)
            {
                var confirmArgs = new GenericQuestionEventArgs(
                    "Upgrade All Packages?",
                    BuildUpgradeConfirmationMessage(standardPackagesNeedingUpdate),
                    true
                );

                genericQuestionService.RaiseQuestion(confirmArgs);
                if (!await confirmArgs.ResponseTask)
                {
                    return;
                }
            }

            lockoutService.Show("Upgrading all packages...");

            var aurUpdates = packagesNeedingUpdate.Aur;
            if (aurUpdates.Count != 0)
            {
                var aurPackageNames = aurUpdates.Select(p => p.Name).ToList();
                var packageBuilds = await privilegedOperationService.GetAurPackageBuild(aurPackageNames);

                foreach (var pkgbuild in packageBuilds)
                {
                    if (pkgbuild.PkgBuild == null) continue;

                    var buildArgs = new PackageBuildEventArgs($"Displaying Package Build {pkgbuild.Name}",
                        pkgbuild.PkgBuild);
                    genericQuestionService.RaisePackageBuild(buildArgs);

                    if (!await buildArgs.ResponseTask)
                    {
                        return;
                    }
                }
            }

            var upgradeResult = await privilegedOperationService.UpgradeAllAsync();
            await ReloadHomePageData();

            if (upgradeResult.NeedsReboot)
            {
                var rebootArgs = new GenericQuestionEventArgs(
                    "Reboot Required",
                    "A full system reboot is required for updates to take effect.\n\nWould you like to reboot now?",
                    true
                );
                genericQuestionService.RaiseQuestion(rebootArgs);
                if (await rebootArgs.ResponseTask)
                {
                    System.Diagnostics.Process.Start("systemctl", "reboot");
                }
            }
            else if (upgradeResult.FailedServiceRestarts.Count > 0)
            {
                var failureList = string.Join("\n", upgradeResult.FailedServiceRestarts
                    .Select(f => $"  • {f.Service}: {f.Error}"));
                var failArgs = new GenericQuestionEventArgs(
                    "Service Restart Failures",
                    $"The following services failed to restart automatically:\n{failureList}",
                    false
                );
                genericQuestionService.RaiseQuestion(failArgs);
                await failArgs.ResponseTask;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            lockoutService.Hide();
        }
    }

    private async Task ReloadHomePageData()
    {
        var tasks = new List<Task>();

        if (_totalAurLabel is not null)
            tasks.Add(LoadAurTotalData(_totalAurLabel, _cts.Token));
        if (_percentAurLabel is not null)
            tasks.Add(LoadAurPercentData(_percentAurLabel, _cts.Token));
        if (_totalPackageLabel is not null)
            tasks.Add(LoadTotalPackageData(_totalPackageLabel, _cts.Token));
        if (_packagePercentLabel is not null)
            tasks.Add(LoadTotalPackagePercentData(_packagePercentLabel, _cts.Token));
        if (_totalFlatpakLabel is not null)
            tasks.Add(LoadTotalFlatpak(_totalFlatpakLabel, _cts.Token));
        if (_flatpakPercentLabel is not null)
            tasks.Add(LoadPercentFlatpak(_flatpakPercentLabel, _cts.Token));
        if (_updatesListBox is not null)
            tasks.Add(LoadUpdatesPanel(_updatesListBox, _cts.Token));
        if (_operationLogListBox is not null)
            tasks.Add(LoadOperationLog(_cts.Token));

        tasks.Add(LoadArchNews(_cts.Token));

        await Task.WhenAll(tasks);
    }

    private static string BuildUpgradeConfirmationMessage(IEnumerable<SyncPackageModel> packages)
    {
        var packageList = packages.ToList();
        if (packageList.Count == 0)
        {
            return string.Empty;
        }

        const int maxPackageColumnWidth = 28;
        var packageColumnWidth = Math.Min(
            maxPackageColumnWidth,
            packageList.Max(package => package.Name.Length));

        return string.Join(Environment.NewLine, packageList.Select(package =>
            $"{FormatPackageName(package.Name, packageColumnWidth)}  {package.OldVersion} -> {package.Version}"));
    }

    private static string FormatPackageName(string packageName, int width)
    {
        if (packageName.Length > width)
        {
            var truncatedWidth = Math.Max(1, width - 1);
            packageName = packageName[..truncatedWidth] + "…";
        }

        return packageName.PadRight(width);
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

    private void OpenCacheCleanDialog()
    {
        try
        {
            var cacheDir = "/var/cache/pacman/pkg";
            if (!Directory.Exists(cacheDir))
            {
                var toastArgs = new ToastMessageEventArgs("Cache directory does not exist");
                genericQuestionService.RaiseToastMessage(toastArgs);
                return;
            }

            var dialogEventArgs = new GenericDialogEventArgs(new Box());

            var content = CacheCleanDialog.BuildContent(
                cacheDir,
                onClean: (keep, uninstalledOnly) =>
                {
                    dialogEventArgs.SetResponse(true);
                    _ = ExecuteCacheClean(keep, uninstalledOnly);
                },
                onCancel: () => dialogEventArgs.SetResponse(false)
            );

            GenericOverlay.ShowGenericOverlay(_overlay, content, dialogEventArgs, 650, 550);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static string StripAnsiAndMarkup(string input)
    {
        var noAnsi = System.Text.RegularExpressions.Regex.Replace(input, @"\x1B\[[^@-~]*[@-~]", "");
        var noMarkup = System.Text.RegularExpressions.Regex.Replace(noAnsi, @"\[\/?[a-zA-Z0-9_ ]*\]", "");
        return noMarkup.Trim();
    }

    private async Task ExecuteCacheClean(int keep, bool uninstalledOnly)
    {
        try
        {
            lockoutService.Show("Cleaning package cache...");
            var result = await privilegedOperationService.RunCacheCleanAsync(keep, uninstalledOnly);

            string message;
            if (result.Success)
            {
                var output = StripAnsiAndMarkup(result.Output ?? "");
                message = string.IsNullOrWhiteSpace(output)
                    ? "Package cache cleaned successfully"
                    : output;
            }
            else
            {
                message = $"Cache clean failed: {result.Error}";
            }

            var toastArgs = new ToastMessageEventArgs(message);
            genericQuestionService.RaiseToastMessage(toastArgs);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            lockoutService.Hide();
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

        PreLoadIcons(packages.Select(x => x.Name).ToList());

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

    private void PreLoadIcons(List<string> icons)
    {
        Task.Run(() =>
        {
            iconResolverService.PreloadIcons(icons);
            return Task.CompletedTask;
        });
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

    private async Task LoadUpdatesPanel(ListBox listBox, CancellationToken ct)
    {
        try
        {
            var updates = await unprivilegedOperationService.CheckForApplicationUpdates();
            var count = updates.Packages.Count + updates.Flatpaks.Count + updates.Aur.Count;
            _availableUpdatesLabel!.SetText($"Available Updates ({count.ToString()})");

            ct.ThrowIfCancellationRequested();

            GLib.Functions.IdleAdd(0, () =>
            {
                while (listBox.GetFirstChild() is { } child)
                    listBox.Remove(child);

                foreach (var pkg in updates.Packages)
                {
                    var row = new ListBoxRow();
                    var label = Label.New($"{pkg.Name}: {pkg.OldVersion} → {pkg.Version}");
                    label.Halign = Align.Start;
                    label.Wrap = true;
                    label.MarginStart = 8;
                    label.MarginEnd = 8;
                    label.MarginTop = 4;
                    label.MarginBottom = 4;
                    row.SetActivatable(false);
                    row.SetChild(label);
                    listBox.Append(row);
                }

                foreach (var pkg in updates.Aur)
                {
                    var row = new ListBoxRow();
                    var label = Label.New($"[AUR] {pkg.Name}: {pkg.OldVersion} → {pkg.Version}");
                    label.Halign = Align.Start;
                    label.Wrap = true;
                    label.MarginStart = 8;
                    label.MarginEnd = 8;
                    label.MarginTop = 4;
                    label.MarginBottom = 4;
                    row.SetActivatable(false);
                    row.SetChild(label);
                    listBox.Append(row);
                }

                foreach (var pkg in updates.Flatpaks)
                {
                    var row = new ListBoxRow();
                    var label = Label.New($"[Flatpak] {pkg.Name ?? pkg.Id}: {pkg.Version}");
                    label.Halign = Align.Start;
                    label.Wrap = true;
                    label.MarginStart = 8;
                    label.MarginEnd = 8;
                    label.MarginTop = 4;
                    label.MarginBottom = 4;
                    row.SetActivatable(false);
                    row.SetChild(label);
                    listBox.Append(row);
                }

                if (updates.Packages.Count == 0 && updates.Aur.Count == 0 && updates.Flatpaks.Count == 0)
                {
                    var row = new ListBoxRow();
                    var label = Label.New("All packages are up to date");
                    label.Halign = Align.Center;
                    label.AddCssClass("dim-label");
                    label.MarginTop = 8;
                    label.MarginBottom = 8;
                    row.SetActivatable(false);
                    row.SetChild(label);
                    listBox.Append(row);
                }

                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task FindNewNews(CancellationToken ct)
    {
        try
        {
            var items = await archNewsService.FindNewNewsAsync(ct);
            ct.ThrowIfCancellationRequested();

            if (items.Count is > 0 and < 10)
            {
                _newArchNewsItems = items;
                ShowNewNews();
                _ = LoadArchNews(_cts.Token); //load news to refresh status so we dont reprompt
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load Arch News: {e.Message}");
        }
    }

    private async Task LoadArchNews(CancellationToken ct)
    {
        try
        {
            var items = await archNewsService.FetchNewsAsync(ct);
            ct.ThrowIfCancellationRequested();

            _archNewsItems = items.Take(10).ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load Arch News: {e.Message}");
        }
    }

    private async Task LoadOperationLog(CancellationToken ct)
    {
        try
        {
            _logEntries = await operationLogService.GetRecentOperationsAsync(8);
            ct.ThrowIfCancellationRequested();

            GLib.Functions.IdleAdd(0, () =>
            {
                if (_operationLogListBox is null) return false;

                while (_operationLogListBox.GetFirstChild() is { } child)
                    _operationLogListBox.Remove(child);

                if (_logEntries.Count == 0)
                {
                    var placeholder = Label.New("No recent activity");
                    placeholder.AddCssClass("dim-label");
                    placeholder.Halign = Align.Center;
                    placeholder.MarginTop = 20;
                    var row = new ListBoxRow();
                    row.SetActivatable(false);
                    row.SetChild(placeholder);
                    _operationLogListBox.Append(row);
                    return false;
                }

                foreach (var entry in _logEntries)
                {
                    var row = new ListBoxRow();
                    row.SetActivatable(true);

                    var hbox = Box.New(Orientation.Horizontal, 10);
                    hbox.MarginStart = 5;
                    hbox.MarginEnd = 5;
                    hbox.MarginTop = 4;
                    hbox.MarginBottom = 4;

                    var icon = Image.NewFromIconName(GetIconForCommand(entry.Command));
                    icon.SetPixelSize(16);
                    hbox.Append(icon);

                    var cmdLabel = Label.New(entry.Command);
                    cmdLabel.SetXalign(0);
                    cmdLabel.Hexpand = true;
                    cmdLabel.Ellipsize = Pango.EllipsizeMode.End;
                    hbox.Append(cmdLabel);

                    var timeLabel = Label.New(FormatRelativeTime(entry.Timestamp));
                    timeLabel.AddCssClass("dim-label");
                    timeLabel.AddCssClass("caption");
                    hbox.Append(timeLabel);

                    if (entry.ExitCode.HasValue)
                    {
                        var statusIcon = Image.NewFromIconName(
                            entry.ExitCode == 0 ? "emblem-ok-symbolic" : "dialog-error-symbolic");
                        statusIcon.SetPixelSize(16);
                        hbox.Append(statusIcon);
                    }
                    else
                    {
                        var inProgressLabel = Label.New("⏳");
                        hbox.Append(inProgressLabel);
                    }

                    row.SetChild(hbox);
                    _operationLogListBox.Append(row);
                }

                if (_logRowActivatedHandler is not null)
                    _operationLogListBox.OnRowActivated -= _logRowActivatedHandler;

                _logRowActivatedHandler = (sender, args) => OnLogRowActivated(args.Row, _logEntries);
                _operationLogListBox.OnRowActivated += _logRowActivatedHandler;

                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load operation log: {e.Message}");
        }
    }

    private async Task OnLogRowActivated(ListBoxRow row, List<OperationLogEntry> entries)
    {
        var index = row.GetIndex();
        if (index < 0 || index >= entries.Count) return;

        var entry = entries[index];

        _logLines = await operationLogService.GetSessionExcerptAsync(entry, MaxRawLineBytes);

        if (_logLines.Count == 0)
        {
            genericQuestionService.RaiseToastMessage(
                new ToastMessageEventArgs("Session log is too large to display")
            );
            return;
        }

        GLib.Functions.IdleAdd(0, () =>
        {
            try
            {
                var fullLogText = string.Join("\n", _logLines);

                _logBox = new Box();
                _logBox.SetOrientation(Orientation.Vertical);
                _logBox.SetSpacing(10);
                _logBox.SetMarginTop(10);
                _logBox.SetMarginBottom(10);
                _logBox.SetMarginStart(10);
                _logBox.SetMarginEnd(10);

                var titleLabel = Label.New("Session Log");
                titleLabel.AddCssClass("title-1");
                titleLabel.Xalign = 0;
                _logBox.Append(titleLabel);

                var textView = new TextView();
                textView.Editable = false;
                textView.WrapMode = WrapMode.WordChar;

                var buffer = textView.Buffer;

                buffer?.SetText(fullLogText, -1);

                var scrolledWindow = new ScrolledWindow();
                scrolledWindow.SetVexpand(true);
                scrolledWindow.HscrollbarPolicy = PolicyType.Automatic;
                scrolledWindow.SetChild(textView);
                _logBox.Append(scrolledWindow);

                var copyButton = Button.NewWithLabel("Copy Log");
                copyButton.Halign = Align.Start;

                copyButton.OnClicked += (_, _) =>
                {
                    var text = fullLogText;

                    var clipboard = Gdk.Display.GetDefault().GetClipboard();
                    clipboard.SetText(text);

                    genericQuestionService.RaiseToastMessage(
                        new ToastMessageEventArgs("Log copied to clipboard")
                    );
                };

                _logBox.Append(copyButton);

                _args = new GenericDialogEventArgs(_logBox);
                GenericOverlay.ShowGenericOverlay(_overlay, _logBox, _args, 700, 500);

                _activeSessionLogOverlay = _logBox;
            }
            catch (Exception e)
            {
            }

            return false;
        });
    }


    private static string GetIconForCommand(string command)
    {
        if (command.Contains("sync", StringComparison.OrdinalIgnoreCase))
            return "emblem-synchronizing-symbolic";
        if (command.Contains("install", StringComparison.OrdinalIgnoreCase))
            return "list-add-symbolic";
        if (command.Contains("remove", StringComparison.OrdinalIgnoreCase))
            return "list-remove-symbolic";
        if (command.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("update", StringComparison.OrdinalIgnoreCase))
            return "software-update-available-symbolic";
        return "utilities-terminal-symbolic";
    }

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 2) return "yesterday";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
        return timestamp.ToString("MMM d");
    }

    public void Dispose()
    {
        if (_updateTimerId > 0)
        {
            GLib.Functions.SourceRemove(_updateTimerId);
            _updateTimerId = 0;
        }

        _logBox?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _logEntries?.Clear();
        _logEntries = null;
        _logLines.Clear();
        _logLines = null;
        _args = null!;
    }
}