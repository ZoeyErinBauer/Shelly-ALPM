using GObject;
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.AUR.GObjects;

// ReSharper disable NotAccessedField.Local

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows.AUR;

public class AurRemove(
    IPrivilegedOperationService privilegedOperationService,
    ILockoutService lockoutService,
    IConfigService configService,
    IGenericQuestionService genericQuestionService,
    IDirtyService dirtyService)
    : IShellyWindow, IReloadable
{
    private DirtySubscription? _sub;
    public string[] ListensTo => [DirtyScopes.AurInstalled];
    private Box _box = null!;
    private readonly CancellationTokenSource _cts = new();
    private ColumnView _columnView = null!;
    private SingleSelection _selectionModel = null!;
    private Gio.ListStore _listStore = null!;
    private string _searchText = string.Empty;
    private FilterListModel _filterListModel = null!;
    private CustomFilter _filter = null!;
    private SignalListItemFactory _checkFactory = null!;
    private SignalListItemFactory _nameFactory = null!;
    private SignalListItemFactory _versionFactory = null!;
    private Box _detailBox = null!;
    private AurPackageGObject? _currentDetailPkg;
    private Revealer _detailRevealer = null!;

    private Dictionary<ColumnViewCell, (SignalHandler<CheckButton> OnToggled, EventHandler OnExternalToggle)>
        _checkBinding = [];

    private readonly List<AurPackageGObject> _packageGObjectRefs = [];
    private ColumnViewColumn _checkColumn = null!;
    private ColumnViewColumn _nameColumn = null!;
    private ColumnViewColumn _versionColumn = null!;
    private Button _removeButton = null!;
    private CheckButton _cascadeDeleteCheck = null!;
    private CheckButton _showHiddenCheck = null!;


    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/AUR/RemoveAurWindow.ui"), -1);
        _box = (Box)builder.GetObject("RemoveAurWindow")!;
        _columnView = (ColumnView)builder.GetObject("package_grid")!;
        var searchEntry = (SearchEntry)builder.GetObject("search_entry")!;

        _checkColumn = (ColumnViewColumn)builder.GetObject("check_column")!;
        _nameColumn = (ColumnViewColumn)builder.GetObject("name_column")!;
        _versionColumn = (ColumnViewColumn)builder.GetObject("version_column")!;
        _versionColumn.Resizable = true;

        _detailBox = (Box)builder.GetObject("detail_box")!;
        _detailRevealer = (Revealer)builder.GetObject("detail_revealer")!;

        _removeButton = (Button)builder.GetObject("remove_button")!;
        _removeButton.SetSensitive(false);
        _cascadeDeleteCheck = (CheckButton)builder.GetObject("cascade_delete_check")!;
        _showHiddenCheck = (CheckButton)builder.GetObject("show_hidden_check")!;
        _listStore = Gio.ListStore.New(AurPackageGObject.GetGType());
        _filter = CustomFilter.New(FilterPackage);
        _filterListModel = FilterListModel.New(_listStore, _filter);
        _selectionModel = SingleSelection.New(_filterListModel);
        _selectionModel.CanUnselect = true;
        _selectionModel.Autoselect = false;
        _columnView.SetModel(_selectionModel);

        SetupColumns(_checkColumn, _nameColumn, _versionColumn);

        ColumnViewHelper.AlignColumnHeader(_columnView, 1, Align.Start);

        _columnView.OnRealize += (_, _) => { _ = LoadDataAsync(_cts.Token); };
        _columnView.OnActivate += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            if (item is AurPackageGObject pkgObj)
            {
                pkgObj.ToggleSelection();
            }
        };
        searchEntry.OnSearchChanged += (_, _) =>
        {
            _searchText = searchEntry.GetText();
            ApplyFilter();
        };
        _removeButton.OnClicked += (_, _) => { _ = RemovePackagesAsync(); };
        _showHiddenCheck.OnToggled += (_, _) => { _ = LoadDataAsync(_cts.Token); };
        _sub = DirtySubscription.Attach(dirtyService, this);

        _selectionModel.OnSelectionChanged += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            _detailRevealer.SetTransitionType(RevealerTransitionType.SlideLeft);
            if (item is AurPackageGObject pkgObj)
            {
                ShowPackageDetails(pkgObj);
            }
            else
            {
                _detailRevealer.SetTransitionType(RevealerTransitionType.SlideLeft);
                _detailRevealer.SetRevealChild(false);
                _currentDetailPkg = null;
            }
        };

        return _box;
    }

    private bool FilterPackage(GObject.Object obj)
    {
        if (obj is not AurPackageGObject pkgObj || pkgObj.Package == null)
            return false;

        return string.IsNullOrWhiteSpace(_searchText) ||
               pkgObj.Package.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        _filter.Changed(FilterChange.Different);
    }

    private void SetupColumns(ColumnViewColumn checkColumn, ColumnViewColumn nameColumn, ColumnViewColumn versionColumn)
    {
        var checkFactory = _checkFactory = SignalListItemFactory.New();
        checkFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var check = CheckButton.New();
            check.MarginStart = 10;
            check.MarginEnd = 10;
            listItem.SetChild(check);
        };

        checkFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AurPackageGObject pkgObj ||
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

        checkFactory.OnUnbind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AurPackageGObject pkgObj ||
                listItem.GetChild() is not CheckButton checkButton) return;
            if (_checkBinding.Remove(listItem, out var handlers))
            {
                pkgObj.OnSelectionToggled -= handlers.OnExternalToggle;
                checkButton.OnToggled -= handlers.OnToggled;
            }
        };

        checkColumn.SetFactory(checkFactory);

        var nameFactory = _nameFactory = SignalListItemFactory.New();
        nameFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        nameFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AurPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Name);
            label.Halign = Align.Start;
        };
        nameColumn.SetFactory(nameFactory);

        var versionFactory = _versionFactory = SignalListItemFactory.New();
        versionFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        versionFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AurPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Version);
            label.Halign = Align.End;
        };
        versionColumn.SetFactory(versionFactory);
    }

    private async Task LoadDataAsync(CancellationToken ct = default)
    {
        try
        {
            var packages = await privilegedOperationService.GetAurInstalledPackagesAsync(_showHiddenCheck.Active);
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($@"[DEBUG_LOG] Loaded {packages.Count} installed packages");

            GLib.Functions.IdleAdd(0, () =>
            {
                _listStore.RemoveAll();
                _packageGObjectRefs.Clear();
                foreach (var gobject in packages.Select(dto =>
                         {
                             var o = AurPackageGObject.NewWithProperties([]);
                             o.Package = dto;
                             o.IsSelected = false;
                             return o;
                         }))
                {
                    _packageGObjectRefs.Add(gobject);
                    _listStore.Append(gobject);
                }

                return false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($@"Failed to load installed packages for removal: {e.Message}");
        }
    }

    private async Task RemovePackagesAsync()
    {
        var selectedPackages = new List<string>();
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            var item = _listStore.GetObject(i);
            if (item is AurPackageGObject { IsSelected: true, Package: not null } pkgObj)
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
                //do work
                var result =
                    await privilegedOperationService.RemoveAurPackagesAsync(selectedPackages,
                        _cascadeDeleteCheck.Active);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to remove packages: {result.Error}");
                }
                else
                {
                    var args = new ToastMessageEventArgs(
                        $"Removed {selectedPackages.Count} Package(s)"
                    );
                    genericQuestionService.RaiseToastMessage(args);
                }

                await LoadDataAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to remove packages: {e.Message}");
            }
            finally
            {
                lockoutService.Hide();
            }
        }
    }

    private bool AnySelected()
    {
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            var item = _listStore.GetObject(i);
            if (item is AurPackageGObject { IsSelected: true })
                return true;
        }

        return false;
    }

    private void ShowPackageDetails(AurPackageGObject pkgObj)
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
            _detailRevealer.SetTransitionType(RevealerTransitionType.SlideLeft);
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

        iconImage.SetFromIconName("package-x-generic");

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
        if (pkg.NumVotes > 0)
            AddDetail("Votes", pkg.NumVotes.ToString());
        if (pkg.Popularity > 0)
            AddDetail("Popularity", pkg.Popularity.ToString("F2"));
        if (pkg.OutOfDate != null)
            AddDetail("Out of Date",
                DateTimeOffset.FromUnixTimeSeconds(pkg.OutOfDate.Value).ToString("yyyy-MM-dd"));

        AddDetail("Maintainer", pkg.Maintainer ?? "Orphaned");
        AddDetail("Last Modified", DateTimeOffset.FromUnixTimeSeconds(pkg.LastModified).ToString("yyyy-MM-dd HH:mm"));
        AddDetail("First Submitted",
            DateTimeOffset.FromUnixTimeSeconds(pkg.FirstSubmitted).ToString("yyyy-MM-dd HH:mm"));
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

        if (pkg.Depends?.Count > 0)
        {
            AddChipList("Depends", pkg.Depends);
        }

        if (pkg.MakeDepends?.Count > 0)
        {
            AddChipList("Make Depends", pkg.MakeDepends);
        }

        if (pkg.CheckDepends?.Count > 0)
        {
            AddChipList("Check Depends", pkg.CheckDepends);
        }

        if (pkg.OptDepends?.Count > 0)
        {
            AddChipList("Optional Deps", pkg.OptDepends, true);
        }

        if (pkg.License?.Count > 0)
        {
            AddChipList("License", pkg.License);
        }

        if (pkg.Keywords?.Count > 0)
        {
            AddChipList("Keywords", pkg.Keywords);
        }


        if (pkg.Provides?.Count > 0)
            AddChipList("Provides", pkg.Provides);
        if (pkg.Conflicts?.Count > 0)
            AddChipList("Conflicts", pkg.Conflicts);
        if (pkg.Groups?.Count > 0)
            AddChipList("Groups", pkg.Groups);
        if (pkg.Replaces?.Count > 0)
            AddChipList("Replaces", pkg.Replaces);

        if (configService.LoadConfig().WebViewEnabled)
        {
            if (pkg.Depends?.Count > 0)
            {
                var dictionary = new Dictionary<string, List<string>> { { pkg.Name, pkg.Depends } };

                foreach (var dep in pkg.Depends)
                {
                    for (uint i = 0; i < _listStore.GetNItems(); i++)
                    {
                        var obj = _listStore.GetObject(i);
                        if (obj is not AurPackageGObject depObj || depObj.Package == null) continue;
                        if (depObj.Package.Name.Contains(dep))
                            dictionary.TryAdd(depObj.Package.Name, depObj.Package.Depends ?? []);
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
            var expander = Expander.New($"{label} ({items.Count})");
            expander.AddCssClass("package-detail-expander");
            expander.Hexpand = false;

            var flowBox = FlowBox.New();
            flowBox.SelectionMode = SelectionMode.None;
            flowBox.ColumnSpacing = 6;
            flowBox.RowSpacing = 6;
            flowBox.Halign = Align.Start;
            flowBox.Valign = Align.Start;
            flowBox.MaxChildrenPerLine = isOptional ? 1u : 10u;
            flowBox.MinChildrenPerLine = 1;


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

    public void Reload() => _ = LoadDataAsync(_cts.Token);

    public void Dispose()
    {
        _sub?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _listStore.RemoveAll();
        _packageGObjectRefs.Clear();
        _checkBinding.Clear();
    }
}