using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

namespace Shelly.Gtk.Windows.Packages;

public class PackageUpdate(IPrivilegedOperationService privilegedOperationService, ILockoutService lockoutService, IConfigService configService, IGenericQuestionService genericQuestionService) : IShellyWindow
{
    private Box _box = null!;
    private ColumnView _columnView = null!;
    private SingleSelection _selectionModel = null!;
    private Gio.ListStore _listStore = null!;
    private FilterListModel _filterListModel = null!;
    private CustomFilter _filter = null!;
    private string _searchText = string.Empty;
    private Dictionary<ListItem, EventHandler> _checkBinding = [];
    private SignalListItemFactory _checkFactory = null!;
    private SignalListItemFactory _nameFactory = null!;
    private SignalListItemFactory _oldVersionFactory = null!;
    private SignalListItemFactory _sizeDiffFactory = null!;
    private SignalListItemFactory _versionFactory = null!;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/Package/UpdateWindow.ui"), -1);
        _box = (Box)builder.GetObject("UpdateWindow")!;
        _columnView = (ColumnView)builder.GetObject("package_grid")!;
        var searchEntry = (SearchEntry)builder.GetObject("search_entry")!;

        var checkColumn = (ColumnViewColumn)builder.GetObject("check_column")!;
        var nameColumn = (ColumnViewColumn)builder.GetObject("name_column")!;
        var oldColumn = (ColumnViewColumn)builder.GetObject("old_column")!;
        var versionColumn = (ColumnViewColumn)builder.GetObject("version_column")!;
        var sizeDiffColumn = (ColumnViewColumn)builder.GetObject("size_diff_column")!;
        var refreshButton = (Button)builder.GetObject("sync_button")!;
        var updateButton = (Button)builder.GetObject("update_button")!;

        _listStore = Gio.ListStore.New(AlpmUpdateGObject.GetGType());
        _filter = CustomFilter.New(FilterPackage);
        _filterListModel = FilterListModel.New(_listStore, _filter);
        _selectionModel = SingleSelection.New(_filterListModel);
        _selectionModel.CanUnselect = true;
        _columnView.SetModel(_selectionModel);

        SetupColumns(checkColumn, nameColumn, sizeDiffColumn, oldColumn, versionColumn);

        ColumnViewHelper.AlignColumnHeader(_columnView, 1, Align.End);
        ColumnViewHelper.AlignColumnHeader(_columnView, 2, Align.End);
        ColumnViewHelper.AlignColumnHeader(_columnView, 3, Align.End);

        _columnView.OnRealize += (_, _) => { _ = LoadDataAsync(); };
        _columnView.OnActivate += (_, _) =>
        {
            var item = _selectionModel.GetSelectedItem();
            if (item is AlpmUpdateGObject pkgObj)
            {
                pkgObj.ToggleSelection();
            }
        };
        searchEntry.OnSearchChanged += (_, _) =>
        {
            _searchText = searchEntry.GetText();
            ApplyFilter();
        };
        updateButton.OnClicked += (_, _) => { _ = UpdateSelectedAsync(); };
        refreshButton.OnClicked += (_, _) => { _ = LoadDataAsync(); };
        
        return _box;
    }

    private void SetupColumns(ColumnViewColumn checkColumn, ColumnViewColumn nameColumn, ColumnViewColumn sizeDiffColumn, ColumnViewColumn oldColumn, ColumnViewColumn versionColumn)
    {
        _checkFactory = SignalListItemFactory.New();
        _checkFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var check = new CheckButton { MarginStart = 10, MarginEnd = 10 };
            listItem.SetChild(check);
            
            check.OnToggled += (s, _) =>
            {
                if (listItem.GetItem() is AlpmUpdateGObject pkgObj)
                {
                    pkgObj.IsSelected = s.GetActive();
                }
            };
        };

        _checkFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmUpdateGObject pkgObj ||
                listItem.GetChild() is not CheckButton checkButton) return;

            checkButton.SetActive(pkgObj.IsSelected);

            pkgObj.OnSelectionToggled += OnExternalToggle;
            _checkBinding[listItem] = OnExternalToggle;
            return;

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
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmPackageGObject pkgObj) return;
            if (_checkBinding.Remove(listItem, out var handler))
                pkgObj.OnSelectionToggled -= handler;
        };
        checkColumn.SetFactory(_checkFactory);
        
        _nameFactory = SignalListItemFactory.New();
        _nameFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _nameFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmUpdateGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Name);
            label.Halign = Align.Start;
        };
        nameColumn.SetFactory(_nameFactory);
        
        _sizeDiffFactory = SignalListItemFactory.New();
        _sizeDiffFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _sizeDiffFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmUpdateGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(SizeHelpers.FormatSize(pkg.SizeDifference));
            label.Halign = Align.End;
        };
        sizeDiffColumn.SetFactory(_sizeDiffFactory);
        
        _oldVersionFactory = SignalListItemFactory.New();
        _oldVersionFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _oldVersionFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmUpdateGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.NewVersion);
            label.Halign = Align.End;
        };
        oldColumn.SetFactory(_oldVersionFactory);
        
        _versionFactory = SignalListItemFactory.New();
        _versionFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _versionFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmUpdateGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.CurrentVersion);
            label.Halign = Align.End;
        };
        versionColumn.SetFactory(_versionFactory);
    }

    private bool FilterPackage(GObject.Object obj)
    {
        if (obj is not AlpmUpdateGObject pkgObj || pkgObj.Package == null)
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        return pkgObj.Package.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var packages = await privilegedOperationService.GetPackagesNeedingUpdateAsync();
            GLib.Functions.IdleAdd(0, () =>
            {
                _listStore.RemoveAll();
                foreach (var package in packages)
                {
                    _listStore.Append(new AlpmUpdateGObject { Package = package });
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

    private async Task UpdateSelectedAsync()
    {
        var selectedPackages = new List<string>();
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            var item = _listStore.GetObject(i);
            if (item is AlpmUpdateGObject { IsSelected: true, Package: not null } pkgObj)
            {
                selectedPackages.Add(pkgObj.Package.Name);
            }
        }

        if (selectedPackages.Count != _listStore.GetNItems())
        {
            var args = new GenericQuestionEventArgs(
                "Update Packages?", "It is unadvised to not update all packages at once. Are you sure you want to continue?"
            );
            
            genericQuestionService.RaiseQuestion(args);
            if (!await args.ResponseTask)
            {
                return;
            }
        }
        
        if (selectedPackages.Count != 0)
        {
            if (!configService.LoadConfig().NoConfirm)
            {
                var args = new GenericQuestionEventArgs(
                    "Update Packages?", string.Join("\n", selectedPackages)
                );

                genericQuestionService.RaiseQuestion(args);
                if (!await args.ResponseTask)
                {
                    return;
                }
            }
            
            var isFullUpgrade = selectedPackages.Count == _listStore.GetNItems();
            try
            {
                lockoutService.Show($"Updating...");
                if (isFullUpgrade)
                    await privilegedOperationService.UpgradeSystemAsync();
                else
                    await privilegedOperationService.UpdatePackagesAsync(selectedPackages);

                await LoadDataAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to install packages: {e.Message}");
            }
            finally
            {
                lockoutService.Hide();
            }
        }
    }

    public void Dispose()
    {
        _columnView.Dispose();
        _columnView.SetModel(null);
        
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            if (_listStore.GetObject(i) is not AlpmUpdateGObject pkgObj) continue;
            pkgObj.Package = null;
            pkgObj.Dispose();
        }

        _listStore.RemoveAll();
        
        _searchText = string.Empty;

        _selectionModel.Dispose();
        _filterListModel.Dispose();
        _filter.Dispose();
        _listStore.Dispose();

        _checkBinding.Clear();
        _checkBinding = null!;
        
        _box.Dispose();
        
        _checkFactory.Dispose();
        _nameFactory.Dispose();
        _oldVersionFactory.Dispose();
        _sizeDiffFactory.Dispose();
        _versionFactory.Dispose();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }
}