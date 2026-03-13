using Gtk;
using Shelly.Gtk.Enums;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows.Flatpak;

public class FlatpakInstall(
    IUnprivilegedOperationService unprivilegedOperationService,
    ILockoutService lockoutService,
    IConfigService configService,
    IGenericQuestionService genericQuestionService) : IShellyWindow
{
    private ListView? _listView;
    private readonly CancellationTokenSource _cts = new();
    private Gio.ListStore? _listStore;
    private SingleSelection? _selectionModel;
    private ListBox? _categoryListBox;
    private List<AppstreamApp> _allPackages = [];
    private string _searchText = string.Empty;
    private FlatpakCategories _selectedCategory = FlatpakCategories.AllApplications;
    private SignalListItemFactory? _factory;
    private Box? _overlay;
    private Button _overlayCloseButton = null!;
    private Button _overlayInstallButton= null!;
    private Label _overlayAuthorLabel = null!;
    private Label _overlayNameLabel = null!;
    private Label _overlayVersionLabel = null!;
    private Label _overlaySizeLabel = null!;
    private Label _overlayLicenseLabel = null!;
    private Label _overlayUrlLabel = null!;
    private Label _overlaySummaryLabel = null!;
    private Label _overlayDescriptionLabel = null!;
    private Image _overlayIconImage = null!;
    private Box? _overlayScreenshotsBox = null!;
    
    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/Flatpak/FlatpakInstallWindow.ui"), -1);
        var box = (Box)builder.GetObject("FlatpakInstallWindow")!;

        _listView = (ListView)builder.GetObject("list_flatpaks")!;
        var reloadButton = (Button)builder.GetObject("reload_button")!;
        var searchEntry = (SearchEntry)builder.GetObject("search_entry")!;
        _categoryListBox = (ListBox)builder.GetObject("category_list")!;
        _overlay = (Box)builder.GetObject("overlay_panel")!;
        _overlayScreenshotsBox = (Box)builder.GetObject("overlay_screenshots_box")!;
        _overlayAuthorLabel = (Label)builder.GetObject("overlay_author_label")!;
        _overlayNameLabel  = (Label)builder.GetObject("overlay_name_label")!;
        _overlayVersionLabel  = (Label)builder.GetObject("overlay_version_label")!;
        _overlaySizeLabel  = (Label)builder.GetObject("overlay_size_label")!;
        _overlayLicenseLabel  = (Label)builder.GetObject("overlay_license_label")!;
        _overlayUrlLabel   = (Label)builder.GetObject("overlay_urls_label")!;
        _overlaySummaryLabel  = (Label)builder.GetObject("overlay_summary_label")!;
        _overlayDescriptionLabel = (Label)builder.GetObject("overlay_description_label")!;
        
        _overlayCloseButton = (Button)builder.GetObject("overlay_back_button")!;
        _overlayInstallButton = (Button)builder.GetObject("overlay_install_button")!;

        var categories = Enum.GetNames<FlatpakCategories>();
        foreach (var category in categories)
        {
            var label = new Label();
            label.SetText(category);
            label.Halign = Align.Start;
            _categoryListBox.Append(label);
        }


        _listStore = Gio.ListStore.New(FlatpakGObject.GetGType());

        _selectionModel = SingleSelection.New(_listStore);
        _listView.SetModel(_selectionModel);
        _listView.SingleClickActivate = true;
        
        _factory = SignalListItemFactory.New();
        _factory.OnSetup += OnSetup;
        _factory.OnBind += OnBind;
        _listView.SetFactory(_factory);

        _listView.OnRealize += (_, _) => { _ = LoadDataAsync(_cts.Token); };
      
        reloadButton.OnClicked += (_, _) => { _ = LoadDataAsync(); };
        searchEntry.OnSearchChanged += (_, _) =>
        {
            _searchText = searchEntry.GetText();
            ApplyFilter();
        };

        _categoryListBox.OnRowSelected += (_, args) =>
        {
            if (args.Row is null) return;
            _selectedCategory = (FlatpakCategories)args.Row.GetIndex();
            _overlay.SetVisible(false);
            ApplyFilter();
        };

        _listView.OnActivate += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            if (item is FlatpakGObject pkgObj)
            {
                var obj = pkgObj.Package;

                if (obj == null) return;
                
                _overlayCloseButton.OnClicked += (_, _) => _overlay.SetVisible(false);
                _overlayInstallButton.OnClicked += (_, _) => { _ = InstallSelectedAsync(); };
                
                _overlayIconImage = (Image)builder.GetObject("overlay_icon")!;
                
                _overlayAuthorLabel.SetText(obj.DeveloperName);
                _overlayNameLabel.SetText(obj.Name);
                _overlayVersionLabel.SetText(obj.Releases.First().Version);
                
                _overlayLicenseLabel.SetText(obj.ProjectLicense);
                _overlaySummaryLabel.SetText(obj.Summary);
                _overlayDescriptionLabel.SetText(obj.Description);
                
                SetUrlLinks(obj.Urls);
                
                _overlayIconImage.SetFromFile($"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{obj.Id}.png");

                List<string> images = [];
                
                images.AddRange(obj.Screenshots
                    .Select(screenshot => screenshot.Images.FirstOrDefault()?.Url)
                    .Where(url => !string.IsNullOrEmpty(url))!);

                PopulateScreenshots(images);
                
                _overlay.SetVisible(true);
            }
        };

        return box;
    }
    
    private void SetUrlLinks(Dictionary<string, string>? urls)
    {
        if (urls == null || urls.Count == 0)
        {
            _overlayUrlLabel!.SetText("No links available");
            return;
        }

        var markup = string.Join("  ·  ", urls.Select(kvp =>
            $"<a href=\"{kvp.Value}\">{CapitalizeFirst(kvp.Key)}</a>"
        ));

        _overlayUrlLabel!.SetMarkup(markup);
        _overlayUrlLabel.UseMarkup = true;
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    
    private void PopulateScreenshots(List<string> imageUrls)
    {
        while (_overlayScreenshotsBox!.GetFirstChild() is { } child)
            _overlayScreenshotsBox.Remove(child);

        foreach (var url in imageUrls)
        {
            var picture = Picture.New();
            picture.ContentFit = ContentFit.Cover;
            picture.HeightRequest = 584;
            picture.WidthRequest = 900;
            picture.AddCssClass("card");
            
            _ = Task.Run(async () =>
            {
                try
                {
                    using var http = new HttpClient();
                    var bytes = await http.GetByteArrayAsync(url);
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        var stream = Gio.MemoryInputStream.NewFromBytes(GLib.Bytes.New(bytes));
                        var pixbuf = GdkPixbuf.Pixbuf.NewFromStream(stream, null)!;
                        var texture = Gdk.Texture.NewForPixbuf(pixbuf);

                        var isPortrait = pixbuf.Height > pixbuf.Width;
                        picture.HeightRequest = 584;
                        if (isPortrait)
                        {
                            picture.WidthRequest = (int)(584.0 * pixbuf.Width / pixbuf.Height);
                        }
                        else
                        {
                            picture.WidthRequest = 900;
                        }

                        picture.SetPaintable(texture);
                        return false;
                    });

                }
                catch
                {
                  // if we get an error keep going
                }
            });

            _overlayScreenshotsBox.Append(picture);
        }
    }

    private static void OnSetup(SignalListItemFactory sender, SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = (ListItem)args.Object;
        var hbox = Box.New(Orientation.Horizontal, 10);
        hbox.MarginStart = 10;
        hbox.MarginEnd = 10;
        hbox.MarginTop = 5;
        hbox.MarginBottom = 5;

        var icon = Image.New();
        hbox.Append(icon);

        var vbox = Box.New(Orientation.Vertical, 2);
        var nameLabel = Label.New(string.Empty);
        nameLabel.Halign = Align.Start;
        nameLabel.AddCssClass("heading");

        var idLabel = Label.New(string.Empty);
        idLabel.Halign = Align.Start;
        idLabel.AddCssClass("dim-label");

        vbox.Append(nameLabel);
        vbox.Append(idLabel);
        hbox.Append(vbox);

        var versionLabel = Label.New(string.Empty);
        versionLabel.Halign = Align.End;
        versionLabel.Hexpand = true;
        hbox.Append(versionLabel);

        listItem.SetChild(hbox);
    }

    private void OnBind(SignalListItemFactory sender, SignalListItemFactory.BindSignalArgs args)
    {
        var listItem = (ListItem)args.Object;
        if (listItem.GetItem() is not FlatpakGObject stringObj) return;
        if (listItem.GetChild() is not Box hbox) return;

        var packageId = stringObj.Package?.Id;
        var package = _allPackages.FirstOrDefault(p => p.Id == packageId);
        if (package == null) return;

        var icon = (Image)hbox.GetFirstChild()!;
        var vbox = (Box)icon.GetNextSibling()!;
        var nameLabel = (Label)vbox.GetFirstChild()!;
        var idLabel = (Label)nameLabel.GetNextSibling()!;
        var versionLabel = (Label)vbox.GetNextSibling()!;

        icon.SetFromFile($"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{package.Id}.png");
        
        nameLabel.SetText(package.Name);
        idLabel.SetText(package.Summary);
        versionLabel.SetText(package.Releases.First().Version);
    }

    private async Task LoadDataAsync(CancellationToken ct = default)
    {
        try
        {
            lockoutService.Show("Loading available Flatpak packages...", 0, false);
            await unprivilegedOperationService.FlatpakSyncRemoteAppstream();
            ct.ThrowIfCancellationRequested();
            _allPackages = await unprivilegedOperationService.ListAppstreamFlatpak();
            ct.ThrowIfCancellationRequested();
            GLib.Functions.IdleAdd(0, () =>
            {
                ApplyFilter();
                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load installed packages: {e.Message}");
        }
        finally
        {
            lockoutService.Hide();
        }
    }


    private void ApplyFilter()
    {
        if (_listStore == null) return;

        var filtered = _allPackages.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filtered = filtered.Where(p =>
                p.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (_selectedCategory != FlatpakCategories.AllApplications)
        {
            var categoryName = _selectedCategory.ToString();
            filtered = filtered.Where(p => p.Categories.Contains(categoryName, StringComparer.OrdinalIgnoreCase));
        }

        _listStore.RemoveAll();

        foreach (var package in filtered)
        {
            var gObj = new FlatpakGObject();
            gObj.Package = package;
            _listStore.Append(gObj);
        }
    }

    private async Task InstallSelectedAsync()
    {
        var selectedItem = _selectionModel?.GetSelectedItem();
        if (selectedItem is not FlatpakGObject stringObj) return;

        var packageId = stringObj.Package?.Id;

        if (!configService.LoadConfig().NoConfirm)
        {
            var args = new GenericQuestionEventArgs(
                "Install Package?", packageId ?? string.Empty
            );

            genericQuestionService.RaiseQuestion(args);
            if (!await args.ResponseTask)
            {
                return;
            }
        }

        try
        {
            lockoutService.Show($"Installing {packageId}...");
            var result = await unprivilegedOperationService.InstallFlatpakPackage(packageId ?? string.Empty);

            if (!result.Success)
            {
                Console.WriteLine($"Failed to install package {packageId}: {result.Error}");
            }
            else
            {
                await LoadDataAsync();
            }
        }
        finally
        {
            lockoutService.Hide();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _listStore?.RemoveAll();
    }
}