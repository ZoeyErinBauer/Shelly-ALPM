using GObject;
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.Services.Icons;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows.Packages;

public class PackageManagement(
    IPrivilegedOperationService privilegedOperationService,
    ILockoutService lockoutService,
    IConfigService configService,
    IGenericQuestionService genericQuestionService,
    IIconResolverService iconResolverService) : IShellyWindow
{
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

        ColumnViewHelper.AlignColumnHeader(_columnView, 1, Align.End);
        ColumnViewHelper.AlignColumnHeader(_columnView, 2, Align.End);

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
        _groupDropDown.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() == "selected")
            {
                var idx = _groupDropDown.GetSelected();
                var item = (StringObject)_groupDropDown.GetModel()!.GetObject(idx)!;
                _selectedGroup = item.GetString();
                ApplyFilter();
            }
        };

        return _box;
    }

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

        var iconImage = new Image { PixelSize = 64, Halign = Align.Center, MarginBottom = 8 };
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

        void AddChipList(string label, IReadOnlyList<string> items, bool isOptional = false)
        {
            var expander = new Expander { Label = $"{label} ({items.Count})" };
            expander.AddCssClass("package-detail-expander");
            expander.Hexpand = false;

            var flowBox = new FlowBox
            {
                MarginStart = 0,
                MarginTop = 0,
                MarginBottom = 0,
                MarginEnd = 0,
                SelectionMode = SelectionMode.None,
                ColumnSpacing = 6,
                RowSpacing = 6,
                Halign = Align.Start,
                Valign = Align.Start,
                MaxChildrenPerLine = isOptional ? 1u : 10u,
                MinChildrenPerLine = 1
            };

            foreach (var item in items)
            {
                var chip = Label.New(item);
                chip.AddCssClass("package-chip");
                chip.Selectable = true;
                chip.Ellipsize = Pango.EllipsizeMode.End;
                chip.MaxWidthChars = 25;

                if (isOptional)
                {
                    chip.Wrap = true;
                    chip.WrapMode = Pango.WrapMode.WordChar;
                    chip.Xalign = 0;
                }
                flowBox.Append(chip);
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
            var check = new CheckButton { MarginStart = 10, MarginEnd = 10 };
            listItem.SetChild(check);
        };

        _checkFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject pkgObj ||
                listItem.GetChild() is not CheckButton checkButton) return;

            checkButton.SetActive(pkgObj.IsSelected);
            checkButton.OnToggled += OnToggled;

            pkgObj.OnSelectionToggled += OnExternalToggle;
            _checkBinding[listItem] = (OnToggled, OnExternalToggle);

            return;

            void OnToggled(CheckButton s, EventArgs e)
            {
                pkgObj.IsSelected = s.GetActive();
                _removeButton.SetSensitive(AnySelected());
            }

            void OnExternalToggle(object? s, EventArgs e)
            {
                if (listItem.GetItem() == pkgObj)
                {
                    checkButton.SetActive(pkgObj.IsSelected);
                }
            }
        };

        _checkFactory.OnUnbind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject pkgObj ||
                listItem.GetChild() is not CheckButton checkButton) return;
            if (_checkBinding.Remove(listItem, out var handlers))
            {
                pkgObj.OnSelectionToggled -= handlers.OnExternalToggle;
                checkButton.OnToggled -= handlers.OnToggled;
            }
        };
        checkColumn.SetFactory(_checkFactory);

        _nameFactory = SignalListItemFactory.New();
        _nameFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var box = Box.New(Orientation.Horizontal, 6);
            var packageIcon = new Image { PixelSize = 24 };
            var label = Label.New(string.Empty);

            box.Append(packageIcon);
            box.Append(label);
            listItem.SetChild(box);
        };
        _nameFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Box box) return;

            var packageIcon = (Image)box.GetFirstChild()!;
            var label = (Label)packageIcon.GetNextSibling()!;

            var iconPath = iconResolverService.GetIconPath(pkg.Name);
            if (!string.IsNullOrEmpty(iconPath) && iconPath != "Unavailable" && File.Exists(iconPath))
            {
                var texture = Gdk.Texture.NewFromFilename(iconPath);
                packageIcon.SetFromPaintable(texture);
                packageIcon.Visible = true;
            }
            else
            {
                packageIcon.SetFromIconName("package-x-generic");
                packageIcon.Visible = true;
            }

            label.SetText(pkg.Name);
            label.Halign = Align.Start;
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
        };

        versionColumn.SetFactory(_versionFactory);
    }

    private bool FilterPackage(GObject.Object obj)
    {
        if (obj is AlpmPackageGObject pkgObj && pkgObj.Package != null)
        {
            if (_selectedGroup != "Any" && !pkgObj.Package.Groups.Contains(_selectedGroup))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            return pkgObj.Package.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                   pkgObj.Package.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private async Task LoadDataAsync(CancellationToken ct = default)
    {
        try
        {
            _packages = await privilegedOperationService.GetInstalledPackagesAsync(_showHiddenCheck.Active);
            _groups = _packages.SelectMany(x => x.Groups).Distinct().ToList();
            _groups.Insert(0, "Any");
            _groupsStringList = StringList.New(_groups.ToArray());
            _groupDropDown.SetModel(_groupsStringList);
            ct.ThrowIfCancellationRequested();
            GLib.Functions.IdleAdd(0, () =>
            {
                _listStore.RemoveAll();
                _packageGObjectRefs.Clear();
                foreach (var package in _packages)
                {
                    var pkgObj = new AlpmPackageGObject { Package = package };
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
        _cts.Cancel();
        _cts.Dispose();

        _listStore.RemoveAll();
        _packageGObjectRefs.Clear();
        _checkBinding.Clear();
        _packages.Clear();
        _groups.Clear();
    }
}