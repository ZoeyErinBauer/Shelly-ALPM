using System.Globalization;
using GObject;
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Enums;
using static Shelly.Gtk.Helpers.AurColumnViewSorter;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.AUR.GObjects;
using Shelly.Gtk.Windows.Dialog;

// ReSharper disable NotAccessedField.Local

// ReSharper disable CollectionNeverQueried.Local

namespace Shelly.Gtk.Windows.AUR;

public class AurInstall(
    IPrivilegedOperationService privilegedOperationService,
    ILockoutService lockoutService,
    IConfigService configService,
    IGenericQuestionService genericQuestionService,
    IDirtyService dirtyService) : IShellyWindow, IReloadable
{
    private DirtySubscription? _sub;
    public string[] ListensTo => [DirtyScopes.AurInstalled, DirtyScopes.Config];
    private Box _box = null!;
    private CancellationTokenSource _cts = new();
    private int _loadGeneration;
    private ColumnView _columnView = null!;
    private SingleSelection _selectionModel = null!;
    private Gio.ListStore _listStore = null!;
    private string _searchText = string.Empty;
    private SearchEntry _searchEntry = null!;
    private SignalListItemFactory _checkFactory = null!;
    private SignalListItemFactory _nameFactory = null!;
    private SignalListItemFactory _votesFactory = null!;
    private SignalListItemFactory _popFactory = null!;
    private SignalListItemFactory _versionFactory = null!;
    private ColumnViewSorter _columnViewSorter = null!;
    private Box _detailBox = null!;
    private AurPackageGObject? _currentDetailPkg;
    private Revealer _detailRevealer = null!;

    private Dictionary<ColumnViewCell, (SignalHandler<CheckButton> OnToggled, EventHandler OnExternalToggle)>
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        _checkBinding = [];

    private readonly List<AurPackageGObject> _packageGObjectRefs = [];
    private ColumnViewColumn _checkColumn = null!;
    private ColumnViewColumn _nameColumn = null!;
    private ColumnViewColumn _votesColumn = null!;
    private ColumnViewColumn _popColumn = null!;
    private ColumnViewColumn _versionColumn = null!;
    private Button _installButton = null!;
    private CheckButton _chrootCheck = null!;
    private CheckButton _runChecksCheck = null!;

    private Label _searchForPackageLabel = null!;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/AUR/AurWindow.ui"), -1);
        _box = (Box)builder.GetObject("AurInstallWindow")!;
        _columnView = (ColumnView)builder.GetObject("package_grid")!;
        _searchEntry = (SearchEntry)builder.GetObject("search_entry")!;

        _checkColumn = (ColumnViewColumn)builder.GetObject("check_column")!;
        _checkColumn.Resizable = true;

        _nameColumn = (ColumnViewColumn)builder.GetObject("name_column")!;
        _nameColumn.Resizable = true;

        _votesColumn = (ColumnViewColumn)builder.GetObject("votes_column")!;
        _votesColumn.Resizable = true;

        _popColumn = (ColumnViewColumn)builder.GetObject("popularity_column")!;
        _popColumn.Resizable = true;

        _detailBox = (Box)builder.GetObject("detail_box")!;
        _detailRevealer = (Revealer)builder.GetObject("detail_revealer")!;

        _versionColumn = (ColumnViewColumn)builder.GetObject("version_column")!;
        _versionColumn.Resizable = true;
        _installButton = (Button)builder.GetObject("install_button")!;
        _installButton.SetSensitive(false);
        _chrootCheck = (CheckButton)builder.GetObject("chroot_check")!;
        _runChecksCheck = (CheckButton)builder.GetObject("run_checks_check")!;
        _listStore = Gio.ListStore.New(AurPackageGObject.GetGType());
        _selectionModel = SingleSelection.New(_listStore);
        _selectionModel.CanUnselect = true;
        _selectionModel.Autoselect = true;
        _columnView.SetModel(_selectionModel);
        _searchForPackageLabel = (Label)builder.GetObject("search_overlay")!;
        _searchForPackageLabel.Label_ = "<span size='large'>Search for AUR packages</span>";
        _searchForPackageLabel.Visible = true;

        SetupColumns(_checkColumn, _nameColumn, _votesColumn, _popColumn, _versionColumn);

        // Creating sorter
        _nameColumn.Sorter = CustomSorter.New<AurPackageGObject>((a, b) => 0);
        _versionColumn.Sorter = CustomSorter.New<AurPackageGObject>((a, b) => 0);
        
        _columnViewSorter = (ColumnViewSorter)_columnView.GetSorter()!;

        _columnViewSorter.OnChanged += (_, _) =>
        {
            var primaryColumn =
                _columnViewSorter.GetPrimarySortColumn();
            
            if (primaryColumn is null)
                return;
            
            var sortColumn = GetSortColumn(primaryColumn);
            
            var order =
                _columnViewSorter.GetPrimarySortOrder();
            
            if (sortColumn is null)
                return;

            Sort(
                _listStore,
                _packageGObjectRefs,
                sortColumn.Value,
                order
            );
        };        
        
        ColumnViewHelper.AlignColumnHeader(_columnView, 1, Align.Start);
        ColumnViewHelper.AlignColumnHeader(_columnView, 2, Align.End);
        ColumnViewHelper.AlignColumnHeader(_columnView, 3, Align.End);

        _columnView.OnActivate += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            if (item is AurPackageGObject pkgObj)
            {
                pkgObj.ToggleSelection();
                _installButton.SetSensitive(AnySelected());
            }
        };
        _installButton.OnClicked += (_, _) => { _ = InstallSelectedAsync(); };
        _installButton.CanFocus = true;
        _installButton.ReceivesDefault = true;

        var shortcutController = ShortcutController.New();
        shortcutController.Scope = ShortcutScope.Global;
        shortcutController.PropagationPhase = PropagationPhase.Capture;

        var triggers = new[] { "Return", "KP_Enter", "space" };
        foreach (var triggerStr in triggers)
        {
            var action = CallbackAction.New((_, _) =>
            {
                if (!_installButton.GetSensitive()) return false;
                if (OverlayHelper.HasActiveOverlay(_box)) return false;

                Task.Run(async () => await InstallSelectedAsync());
                return true;
            });
            shortcutController.AddShortcut(Shortcut.New(ShortcutTrigger.ParseString(triggerStr), action));
        }

        _box.AddController(shortcutController);

        _searchEntry.OnActivate += (_, _) => { Interlocked.Increment(ref _loadGeneration); _ = SearchAsync(_cts.Token, _loadGeneration); };
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
                _detailRevealer.SetRevealChild(false);
                _currentDetailPkg = null;
            }
        };

        return _box;
    }
    
    private PackageSortColumn? GetSortColumn(ColumnViewColumn column)
    {
        if (column == _nameColumn)
            return PackageSortColumn.Name;
        
        if (column == _versionColumn)
            return PackageSortColumn.Version;

        return null;
    }


    private void SetupColumns(ColumnViewColumn checkColumn, ColumnViewColumn nameColumn,
        ColumnViewColumn votesColumn, ColumnViewColumn popColumn, ColumnViewColumn versionColumn)
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
                _installButton.SetSensitive(AnySelected());
            }

            void OnExternalToggle(object? s, EventArgs e)
            {
                if (listItem.GetItem() == pkgObj)
                {
                    checkButton.SetActive(pkgObj.IsSelected);
                    _installButton.SetSensitive(AnySelected());
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

        var votesFactory = _votesFactory = SignalListItemFactory.New();
        votesFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        votesFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AurPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.NumVotes.ToString(CultureInfo.InvariantCulture));
            label.Halign = Align.End;
        };
        votesColumn.SetFactory(votesFactory);

        var sizeFactory = _popFactory = SignalListItemFactory.New();
        sizeFactory.OnSetup += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        sizeFactory.OnBind += (_, args) =>
        {
            if (args.Object is not ColumnViewCell listItem) return;
            if (listItem.GetItem() is not AurPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Popularity.ToString("F2", CultureInfo.InvariantCulture));
            label.Halign = Align.End;
        };
        popColumn.SetFactory(sizeFactory);

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

    private async Task SearchAsync(CancellationToken ct, int generation = 0)
    {
        _searchText = _searchEntry.GetText();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var result = await privilegedOperationService.SearchAurPackagesAsync(_searchText);
            _searchForPackageLabel.Visible = false;
            ct.ThrowIfCancellationRequested();

            Console.WriteLine($"[DEBUG_LOG] Search result: {result.Count}");

            result = result.OrderByDescending(x => x.NumVotes).ToList();
            GLib.Functions.IdleAdd(0, () =>
            {
                if (ct.IsCancellationRequested || _loadGeneration != generation) return false;

                _listStore.RemoveAll();
                foreach (var r in _packageGObjectRefs) r.Dispose();
                _packageGObjectRefs.Clear();
                foreach (var gobject in result.Select(dto =>
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
    }

    private async Task InstallSelectedAsync()
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
            OperationResult? result;

            try
            {
                if (!configService.LoadConfig().NoConfirm)
                {
                    var args = new GenericQuestionEventArgs(
                        "Install Packages?", string.Join("\n", selectedPackages)
                    );

                    genericQuestionService.RaiseQuestion(args);
                    if (!await args.ResponseTask)
                    {
                        return;
                    }
                }

                lockoutService.Show($"Installing...");

                var packageBuilds = await privilegedOperationService.GetAurPackageBuild(selectedPackages);

                if (packageBuilds.Count == 0)
                {
                    Console.WriteLine("No packages found.");
                    return;
                }

                foreach (var pkgbuild in packageBuilds)
                {
                    if (pkgbuild.PkgBuild == null) continue;

                    var buildArgs =
                        new PackageBuildEventArgs($"Displaying Package Build {pkgbuild.Name}", pkgbuild.PkgBuild);
                    genericQuestionService.RaisePackageBuild(buildArgs);

                    if (!await buildArgs.ResponseTask)
                    {
                        return;
                    }
                }

                result = await privilegedOperationService.InstallAurPackagesAsync(selectedPackages,
                    _chrootCheck.GetActive(), _runChecksCheck.GetActive());
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install packages: {result.Error}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to install packages: {e.Message}");
                result = new OperationResult
                {
                    Success = false,
                    Error = e.ToString(),
                    ExitCode = -1
                };
            }
            finally
            {
                lockoutService.Hide();
            }

            if (result.Success)
            {
                var args = new ToastMessageEventArgs(
                    $"Installed {selectedPackages.Count} Package(s)"
                );

                genericQuestionService.RaiseToastMessage(args);
                return;
            }

            ShowInstallFailureDialog(selectedPackages, result);
        }
    }

    private void ShowInstallFailureDialog(IReadOnlyCollection<string> selectedPackages, OperationResult result)
    {
        var dialogArgs = AurInstallFailureDialog.Create(
            selectedPackages,
            LogHelpers.BuildFailureSummary(result),
            () => ExportInstallLogAsync(selectedPackages, result));

        genericQuestionService.RaiseDialog(dialogArgs);
    }

    private async Task<bool> ExportInstallLogAsync(IReadOnlyCollection<string> selectedPackages, OperationResult result)
    {
        try
        {
            var dialog = FileDialog.New();
            dialog.SetTitle("Export AUR install log");
            dialog.SetInitialName(LogHelpers.CreateSuggestedLogFileName(selectedPackages, "aur"));

            var filter = FileFilter.New();
            filter.SetName("Log Files (*.log)");
            filter.AddPattern("*.log");

            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(filter);
            dialog.SetFilters(filters);

            var file = await dialog.SaveAsync((Window)_box.GetRoot()!);
            if (file is null)
            {
                return false;
            }

            var path = file.GetPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            await File.WriteAllTextAsync(path, LogHelpers.BuildInstallLog(selectedPackages, result, "aur"));

            genericQuestionService.RaiseToastMessage(new ToastMessageEventArgs("Exported AUR install log"));
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to export AUR install log: {e.Message}");
            genericQuestionService.RaiseToastMessage(new ToastMessageEventArgs("Failed to export AUR install log"));
            return false;
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
            AddDetail("Out of Date", DateTimeOffset.FromUnixTimeSeconds(pkg.OutOfDate.Value).ToString("yyyy-MM-dd"));

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

    public void Reload()
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
        Interlocked.Increment(ref _loadGeneration);
        if (!string.IsNullOrEmpty(_searchText))
            _ = SearchAsync(_cts.Token, _loadGeneration);
    }

    public void Dispose()
    {
        _sub?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _listStore.RemoveAll();
        foreach (var r in _packageGObjectRefs) r.Dispose();
        _packageGObjectRefs.Clear();
        _checkBinding.Clear();
    }
}