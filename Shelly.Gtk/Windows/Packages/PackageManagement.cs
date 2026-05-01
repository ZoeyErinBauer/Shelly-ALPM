using GObject;
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.Services.Icons;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

// ReSharper disable NotAccessedField.Local
// ReSharper disable CollectionNeverUpdated.Local

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows.Packages;

public class PackageManagement(
    IPrivilegedOperationService privilegedOperationService,
    ILockoutService lockoutService,
    IConfigService configService,
    IGenericQuestionService genericQuestionService,
    IIconResolverService iconResolverService,
    IDirtyService dirtyService) : IShellyWindow, IReloadable
{
    private DirtySubscription? _sub;
    public string[] ListensTo => [DirtyScopes.Native, DirtyScopes.NativeInstalled];
    private Box _box = null!;
    private readonly CancellationTokenSource _cts = new();
    private ColumnView _columnView = null!;
    private SingleSelection _selectionModel = null!;
    private Gio.ListStore _listStore = null!;
    private FilterListModel _filterListModel = null!;
    private CustomFilter _filter = null!;
    private string _searchText = string.Empty;

    private Dictionary<ColumnViewCell, (SignalHandler<CheckButton> OnToggled, EventHandler OnExternalToggle)>
        _checkBinding = [];

    private SignalListItemFactory _checkFactory = null!;
    private SignalListItemFactory _nameFactory = null!;
    private SignalListItemFactory _sizeFactory = null!;
    private SignalListItemFactory _versionFactory = null!;
    private SearchEntry _searchEntry = null!;
    private CheckButton _cascadeDeleteCheck = null!;
    private CheckButton _removeConfigsCheck = null!;
    private CheckButton _showHiddenCheck = null!;
    private Button _refreshButton = null!;
    private Button _removeButton = null!;
    private readonly List<AlpmPackageGObject> _packageGObjectRefs = [];
    private List<AlpmPackageDto> _packages = [];
    private List<string> _groups = [];
    private StringList _groupsStringList = null!;
    private DropDown _groupDropDown = null!;
    private string _selectedGroup = "Any";

    private ColumnViewColumn _checkColumn = null!;
    private ColumnViewColumn _nameColumn = null!;
    private ColumnViewColumn _sizeColumn = null!;
    private ColumnViewColumn _versionColumn = null!;

    private Revealer _detailRevealer = null!;
    private Box _detailBox = null!;
    private AlpmPackageGObject? _currentDetailPkg;
    private HashSet<string> _installedPackageNames = [];

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/Package/PackageManagement.ui"), -1);
        _box = (Box)builder.GetObject("PackageManagement")!;
        _columnView = (ColumnView)builder.GetObject("package_grid")!;
        _searchEntry = (SearchEntry)builder.GetObject("search_entry")!;
        _cascadeDeleteCheck = (CheckButton)builder.GetObject("cascade_delete_check")!;
        _removeConfigsCheck = (CheckButton)builder.GetObject("remove_configs_check")!;
        _showHiddenCheck = (CheckButton)builder.GetObject("show_hidden_check")!;

        _checkColumn = (ColumnViewColumn)builder.GetObject("check_column")!;
        _checkColumn.Resizable = true;
        _nameColumn = (ColumnViewColumn)builder.GetObject("name_column")!;
        _nameColumn.Resizable = true;
        _sizeColumn = (ColumnViewColumn)builder.GetObject("size_column")!;
        _sizeColumn.Resizable = true;
        _versionColumn = (ColumnViewColumn)builder.GetObject("version_column")!;
        _versionColumn.Resizable = true;

        _refreshButton = (Button)builder.GetObject("sync_button")!;
        _removeButton = (Button)builder.GetObject("remove_button")!;
        _removeButton.SetSensitive(false);

        _listStore = Gio.ListStore.New(AlpmPackageGObject.GetGType());
        _filter = CustomFilter.New(FilterPackage);
        _filterListModel = FilterListModel.New(_listStore, _filter);
        _selectionModel = SingleSelection.New(_filterListModel);
        _selectionModel.CanUnselect = true;
        _selectionModel.Autoselect = false;
        _columnView.SetModel(_selectionModel);
        _groupDropDown = (DropDown)builder.GetObject("grouping_selection")!;
        _detailRevealer = (Revealer)builder.GetObject("detail_revealer")!;
        _detailBox = (Box)builder.GetObject("detail_box")!;

        SetupColumns(_checkColumn, _nameColumn, _sizeColumn, _versionColumn);

        ColumnViewHelper.AlignColumnHeader(_columnView, 1, Align.Start);
        ColumnViewHelper.AlignColumnHeader(_columnView, 2, Align.End);
        ColumnViewHelper.AlignColumnHeader(_columnView, 3, Align.End);

        _columnView.OnRealize += (_, _) => { _ = LoadDataAsync(_cts.Token); };
        _columnView.OnActivate += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            if (item is AlpmPackageGObject pkgObj)
            {
                pkgObj.ToggleSelection();
            }
        };
        _selectionModel.OnSelectionChanged += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            _detailRevealer.SetTransitionType(RevealerTransitionType.SlideLeft);
            if (item is AlpmPackageGObject pkgObj)
            {
                ShowPackageDetails(pkgObj);
            }
            else
            {
                _detailRevealer.SetRevealChild(false);
                _currentDetailPkg = null;
            }
        };
        _searchEntry.OnSearchChanged += (_, _) =>
        {
            _searchText = _searchEntry.GetText();
            ApplyFilter();
        };
        _removeButton.OnClicked += (_, _) => { _ = RemoveSelectedAsync(); };
        _refreshButton.OnClicked += (_, _) => { _ = LoadDataAsync(); };
        _showHiddenCheck.OnToggled += (_, _) => { _ = LoadDataAsync(_cts.Token); };
        _groupDropDown.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "selected")
            {
                var idx = _groupDropDown.GetSelected();
                var item = (StringObject)_groupDropDown.GetModel()!.GetObject(idx)!;
                _selectedGroup = item.GetString();
                ApplyFilter();
            }
        };

        _sub = DirtySubscription.Attach(dirtyService, this);
        return _box;
    }

    public void Reload() => _ = LoadDataAsync(_cts.Token);

    private void ShowPackageDetails(AlpmPackageGObject pkgObj)
    {
        if (pkgObj.Package == null) return;

        _currentDetailPkg = pkgObj;
        var pkg = pkgObj.Package;

        while (_detailBox.GetFirstChild() is { } child)
        {
            _detailBox.Remove(child);
        }

        var backButton = Button.New();
        backButton.SetIconName("go-next-symbolic");
        backButton.Halign = Align.Start;
        backButton.AddCssClass("flat");
        backButton.TooltipText = "Close details";
        backButton.OnClicked += (_, _) =>
        {
            _currentDetailPkg = null;
            _selectionModel.UnselectItem(_selectionModel.GetSelected());
            _detailRevealer.SetTransitionType(RevealerTransitionType.SlideRight);
            _detailRevealer.SetRevealChild(false);
        };
        _detailBox.Append(backButton);

        void AddDetail(string label, string value)
        {
            var row = Box.New(Orientation.Horizontal, 12);
            row.MarginBottom = 4;
            var labelWidget = Label.New(label + ":");
            labelWidget.AddCssClass("dim-label");
            labelWidget.Halign = Align.Start;
            labelWidget.Valign = Align.Start;
            labelWidget.WidthRequest = 80;
            labelWidget.Xalign = 0;

            var valueWidget = Label.New(value);
            valueWidget.Halign = Align.Start;
            valueWidget.Wrap = true;
            valueWidget.WrapMode = Pango.WrapMode.WordChar;
            valueWidget.MaxWidthChars = 30;
            valueWidget.Xalign = 0;
            valueWidget.Selectable = true;

            row.Append(labelWidget);
            row.Append(valueWidget);
            _detailBox.Append(row);
        }

        var headerBox = Box.New(Orientation.Vertical, 4);
        headerBox.MarginBottom = 16;
        headerBox.MarginTop = 8;

        var iconImage = Image.New();
        iconImage.PixelSize = 64;
        iconImage.Halign = Align.Center;
        iconImage.MarginBottom = 8;
        var iconPath = iconResolverService.GetIconPath(pkg.Name);
        if (!string.IsNullOrEmpty(iconPath) && iconPath != "Unavailable" && File.Exists(iconPath))
        {
            var texture = Gdk.Texture.NewFromFilename(iconPath);
            iconImage.SetFromPaintable(texture);
        }
        else
        {
            iconImage.SetFromIconName("package-x-generic");
        }

        headerBox.Append(iconImage);

        var nameLabel = Label.New(pkg.Name);
        nameLabel.AddCssClass("title-2");
        nameLabel.Halign = Align.Center;
        headerBox.Append(nameLabel);

        var descLabel = Label.New(pkg.Description);
        descLabel.AddCssClass("dim-label");
        descLabel.Halign = Align.Center;
        descLabel.Wrap = true;
        descLabel.Justify = Justification.Center;
        descLabel.MaxWidthChars = 40;
        headerBox.Append(descLabel);

        _detailBox.Append(headerBox);

        var separator = Separator.New(Orientation.Horizontal);
        separator.MarginBottom = 16;
        _detailBox.Append(separator);

        AddDetail("Version", pkg.Version);
        AddDetail("Size", SizeHelpers.FormatSize(pkg.InstalledSize));
        if (!string.IsNullOrEmpty(pkg.Url))
        {
            var row = Box.New(Orientation.Horizontal, 12);
            row.MarginBottom = 4;
            var labelWidget = Label.New("URL:");
            labelWidget.AddCssClass("dim-label");
            labelWidget.Halign = Align.Start;
            labelWidget.Valign = Align.Start;
            labelWidget.WidthRequest = 80;
            labelWidget.Xalign = 0;

            var valueWidget = Label.New(null);
            var escaped = GLib.Functions.MarkupEscapeText(pkg.Url, -1);
            valueWidget.SetMarkup($"<a href=\"{escaped}\">{escaped}</a>");
            valueWidget.Halign = Align.Start;
            valueWidget.Wrap = true;
            valueWidget.WrapMode = Pango.WrapMode.WordChar;
            valueWidget.MaxWidthChars = 30;
            valueWidget.Xalign = 0;

            row.Append(labelWidget);
            row.Append(valueWidget);
            _detailBox.Append(row);
        }

        if (pkg.Depends.Count > 0)
        {
            AddChipList("Depends", pkg.Depends);
        }

        if (pkg.OptDepends.Count > 0)
        {
            AddChipList("Optional Deps", pkg.OptDepends, true);
        }

        if (pkg.Licenses.Count > 0)
            AddDetail("Licenses", string.Join(", ", pkg.Licenses));
        if (pkg.Provides.Count > 0)
            AddDetail("Provides", string.Join(", ", pkg.Provides));
        if (pkg.Conflicts.Count > 0)
            AddDetail("Conflicts", string.Join(", ", pkg.Conflicts));
        if (pkg.Groups.Count > 0)
            AddDetail("Groups", string.Join(", ", pkg.Groups));


        if (pkg.PackageFile is { Files.Count: > 0 })
        {
            var fileExpander = Expander.New($"Package Files ({CountFiles(pkg.PackageFile)})");
            fileExpander.AddCssClass("package-detail-expander");
            fileExpander.Hexpand = false;

            var fileBox = Box.New(Orientation.Vertical, 2);
            BuildFileTree(fileBox, pkg.PackageFile.Files, 0);

            var scrolledWindow = ScrolledWindow.New();
            scrolledWindow.SetChild(fileBox);
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolledWindow.HeightRequest = 500;
            scrolledWindow.WidthRequest = 500;

            fileExpander.SetChild(scrolledWindow);

            fileExpander.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "expanded" && fileExpander.GetExpanded())
                    ExpandAllExpanders(fileBox);
            };

            _detailBox.Append(fileExpander);
        }

        if (configService.LoadConfig().WebViewEnabled)
        {
            if (pkg.Depends.Count > 0)
            {
                var dictionary = new Dictionary<string, List<string>> { { pkg.Name, pkg.Depends } };

                foreach (var dep in pkg.Depends)
                {
                    for (uint i = 0; i < _listStore.GetNItems(); i++)
                    {
                        var obj = _listStore.GetObject(i);
                        if (obj is not AlpmPackageGObject depObj || depObj.Package == null) continue;
                        if (depObj.Package.Name.Contains(dep))
                            dictionary.TryAdd(depObj.Package.Name, depObj.Package.Depends);
                    }
                }

                var window = new WebWindow(pkg.Name, dictionary);
                _detailBox.Append(window.CreateWindow());
            }
        }

        _detailRevealer.SetRevealChild(true);
        return;

        int CountFiles(AlpmPackageTreeDto node)
        {
            return node.Files.Count + node.Files.Sum(CountFiles);
        }

        void ExpandAllExpanders(Box container)
        {
            var child = container.GetFirstChild();
            while (child != null)
            {
                if (child is Expander exp)
                {
                    exp.SetExpanded(true);
                    if (exp.GetChild() is Box childBox)
                        ExpandAllExpanders(childBox);
                }

                child = child.GetNextSibling();
            }
        }

        void BuildFileTree(Box container, List<AlpmPackageTreeDto> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                if (node.Files.Count > 0)
                {
                    var dirBox = Box.New(Orientation.Horizontal, 6);
                    dirBox.MarginStart = depth * 16;
                    var folderIcon = Image.NewFromIconName("folder-symbolic");
                    var dirLabel = Label.New(node.Name);
                    dirBox.Append(folderIcon);
                    dirBox.Append(dirLabel);

                    var dirExpander = Expander.New(null);
                    dirExpander.MarginStart = 0;
                    dirExpander.SetLabelWidget(dirBox);
                    var childBox = Box.New(Orientation.Vertical, 2);
                    BuildFileTree(childBox, node.Files, depth + 1);
                    dirExpander.SetChild(childBox);
                    container.Append(dirExpander);
                }
                else
                {
                    var fileBox = Box.New(Orientation.Horizontal, 6);
                    fileBox.MarginStart = depth * 16;
                    var fileIcon = Image.NewFromIconName("text-x-generic-symbolic");
                    var fileLabel = Label.New(node.Name);
                    fileLabel.Halign = Align.Start;
                    fileLabel.Selectable = true;
                    fileLabel.AddCssClass("dim-label");
                    fileBox.Append(fileIcon);
                    fileBox.Append(fileLabel);
                    container.Append(fileBox);
                }
            }
        }


        void AddChipList(string label, IReadOnlyList<string> items, bool isOptional = false)
        {
            var expander = Expander.New($"{label} ({items.Count})");
            expander.AddCssClass("package-detail-expander");
            expander.Hexpand = false;

            var flowBox = FlowBox.New();
            flowBox.MarginStart = 0;
            flowBox.MarginTop = 0;
            flowBox.MarginBottom = 0;
            flowBox.MarginEnd = 0;
            flowBox.SelectionMode = SelectionMode.None;
            flowBox.ColumnSpacing = 6;
            flowBox.RowSpacing = 6;
            flowBox.Halign = Align.Start;
            flowBox.Valign = Align.Start;
            flowBox.MaxChildrenPerLine = isOptional ? 1u : 10u;
            flowBox.MinChildrenPerLine = 1;


            foreach (var item in items)
            {
                if (isOptional)
                {
                    var optDepName = item.Split(':').First().Trim();
                    var isInstalled = _installedPackageNames.Contains(optDepName);

                    var escapedItem = GLib.Functions.MarkupEscapeText(item, -1);

                    var chipBox = Box.New(Orientation.Horizontal, 4);
                    chipBox.AddCssClass("package-chip");
                    chipBox.Valign = Align.Center;

                    var checkIcon = Image.NewFromIconName("object-select-symbolic");
                    checkIcon.PixelSize = 16;
                    checkIcon.Visible = isInstalled;

                    var chipLabel = Label.New(string.Empty);
                    chipLabel.SetMarkup($"<span size='small'>{escapedItem}</span>");
                    chipLabel.Selectable = true;
                    chipLabel.Ellipsize = Pango.EllipsizeMode.End;
                    chipLabel.MaxWidthChars = 25;
                    chipLabel.Wrap = true;
                    chipLabel.WrapMode = Pango.WrapMode.WordChar;
                    chipLabel.Xalign = 0;

                    chipBox.Append(checkIcon);
                    chipBox.Append(chipLabel);
                    flowBox.Append(chipBox);
                }
                else
                {
                    var chip = Label.New(item);
                    chip.AddCssClass("package-chip");
                    chip.Selectable = true;
                    chip.Ellipsize = Pango.EllipsizeMode.End;
                    chip.MaxWidthChars = 25;
                    flowBox.Append(chip);
                }
            }

            expander.SetChild(flowBox);
            _detailBox.Append(expander);
        }
    }

    private void SetupColumns(ColumnViewColumn checkColumn, ColumnViewColumn nameColumn, ColumnViewColumn sizeColumn,
        ColumnViewColumn versionColumn)
    {
        _checkFactory = SignalListItemFactory.New();
        _checkFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var check = CheckButton.New();
            check.MarginStart = 10;
            check.MarginEnd = 10;
            listItem.SetChild(check);

            check.OnToggled += (s, _) =>
            {
                if (listItem.GetItem() is not AlpmPackageGObject current) return;
                current.IsSelected = s.GetActive();
                _removeButton.SetSensitive(AnySelected());
            };
        };

        _checkFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject pkgObj ||
                listItem.GetChild() is not CheckButton checkButton) return;

            checkButton.SetActive(pkgObj.IsSelected);

            pkgObj.OnSelectionToggled += OnExternalToggle;

            return;

            void OnExternalToggle(object? s, EventArgs e)
            {
                if (listItem.GetItem() == pkgObj)
                {
                    checkButton.SetActive(pkgObj.IsSelected);
                }
            }
        };

        _checkFactory.OnUnbind += (_, _) => { };

        _checkFactory.OnTeardown += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject ||
                listItem.GetChild() is not CheckButton ) return;
            listItem.SetChild(null);
        };
        checkColumn.SetFactory(_checkFactory);

        _nameFactory = SignalListItemFactory.New();
        _nameFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var box = Box.New(Orientation.Horizontal, 6);
            
            var packageIcon = Image.New();
            packageIcon.PixelSize = 24;
            var label = Label.New(string.Empty);
            var installedIcon = Image.NewFromIconName("object-select-symbolic");

            box.Append(packageIcon);
            box.Append(label);
            box.Append(installedIcon);
            listItem.SetChild(box);
        };
        _nameFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } pkgObj ||
                listItem.GetChild() is not Box box) return;

            var packageIcon = (Image)box.GetFirstChild()!;
            var label = (Label)packageIcon.GetNextSibling()!;
            var installedIcon = (Image)label.GetNextSibling()!;

            var iconPath = iconResolverService.GetIconPath(pkg.Name);
            if (!string.IsNullOrEmpty(iconPath) && iconPath != "Unavailable" && File.Exists(iconPath))
            {
                packageIcon.SetFromFile(iconPath);
                packageIcon.Visible = true;
            }
            else
            {
                packageIcon.SetFromIconName("package-x-generic");
                packageIcon.Visible = true;
            }

            label.SetText(pkg.Name);
            label.Halign = Align.Start;
            installedIcon.Visible = pkgObj.IsInstalled;
            installedIcon.TooltipText = "Installed";
        };
        nameColumn.SetFactory(_nameFactory);

        _sizeFactory = SignalListItemFactory.New();
        _sizeFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _sizeFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(SizeHelpers.FormatSize(pkg.InstalledSize));
            label.Halign = Align.End;
        };
        sizeColumn.SetFactory(_sizeFactory);

        _versionFactory = SignalListItemFactory.New();
        _versionFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _versionFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Version);
            label.Halign = Align.End;
            label.SetMarginEnd(10);
        };

        versionColumn.SetFactory(_versionFactory);
    }

    private bool FilterPackage(GObject.Object obj)
    {
        if (obj is AlpmPackageGObject { Package: not null } pkgObj)
        {
            if (_selectedGroup != "Any" && !(pkgObj.Package?.Groups.Contains(_selectedGroup) ?? false))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            return (pkgObj.Package?.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (pkgObj.Package?.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return false;
    }

    private async Task LoadDataAsync(CancellationToken ct = default)
    {
        try
        {
            _installedPackageNames.Clear();
            _packages = await privilegedOperationService.GetInstalledPackagesAsync(_showHiddenCheck.Active);
            _groups = _packages.SelectMany(x => x.Groups).Distinct().ToList();
            _groups.Insert(0, "Any");
            _installedPackageNames = new HashSet<string>(_packages.Select(x => x.Name));
            _groupsStringList = StringList.New(_groups.ToArray());
            _groupDropDown.SetModel(_groupsStringList);
            ct.ThrowIfCancellationRequested();
            GLib.Functions.IdleAdd(0, () =>
            {
                _listStore.RemoveAll();
                _packageGObjectRefs.Clear();
                foreach (var package in _packages)
                {
                    var pkgObj = AlpmPackageGObject.NewWithProperties([]);
                    pkgObj.Package = package;
                    _packageGObjectRefs.Add(pkgObj);
                    _listStore.Append(pkgObj);
                }

                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load packages: {e.Message}");
        }
    }

    private void ApplyFilter()
    {
        _filter.Changed(FilterChange.Different);
    }

    private async Task RemoveSelectedAsync()
    {
        var selectedPackages = new List<string>();
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            var item = _listStore.GetObject(i);
            if (item is AlpmPackageGObject { IsSelected: true, Package: not null } pkgObj)
            {
                selectedPackages.Add(pkgObj.Package.Name);
            }
        }

        if (selectedPackages.Count != 0)
        {
            if (!configService.LoadConfig().NoConfirm)
            {
                var args = new GenericQuestionEventArgs(
                    "Remove Packages?", string.Join("\n", selectedPackages)
                );

                genericQuestionService.RaiseQuestion(args);
                if (!await args.ResponseTask)
                {
                    return;
                }
            }

            try
            {
                lockoutService.Show($"Removing...");
                await privilegedOperationService.RemovePackagesAsync(selectedPackages, _cascadeDeleteCheck.Active,
                    _removeConfigsCheck.Active);
                await LoadDataAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to install packages: {e.Message}");
            }
            finally
            {
                lockoutService.Hide();

                var args = new ToastMessageEventArgs(
                    $"Removed {selectedPackages.Count} Package(s)"
                );

                genericQuestionService.RaiseToastMessage(args);
            }
        }
    }

    private bool AnySelected()
    {
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            var item = _listStore.GetObject(i);
            if (item is AlpmPackageGObject { IsSelected: true })
                return true;
        }

        return false;
    }

    public void Dispose()
    {
        _sub?.Dispose();
        _cts.Cancel();
        _cts.Dispose();

        _listStore.RemoveAll();
        _packageGObjectRefs.Clear();
        _checkBinding.Clear();
        _packages.Clear();
        _groups.Clear();
        _installedPackageNames.Clear();
    }
}