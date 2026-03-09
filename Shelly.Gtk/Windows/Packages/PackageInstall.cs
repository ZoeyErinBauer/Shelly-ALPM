using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;
using Shelly.Gtk.UiModels.PackageManagerObjects;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

namespace Shelly.Gtk.Windows.Packages;

public class PackageInstall(IPrivilegedOperationService privilegedOperationService, ILockoutService lockoutService, IConfigService configService, IGenericQuestionService genericQuestionService)
    : IShellyWindow
{
    private Overlay _overlay = null!;
    private readonly CancellationTokenSource _cts = new();
    private ColumnView _columnView = null!;
    private SingleSelection _selectionModel = null!;
    private Gio.ListStore _listStore = null!;
    private FilterListModel _filterListModel = null!;
    private CustomFilter _filter = null!;
    private string _searchText = string.Empty;
    private List<AlpmPackageDto> _packages = [];
    private Dictionary<ListItem, EventHandler> _checkBinding = [];
    private SignalListItemFactory _checkFactory = null!;
    private SignalListItemFactory _nameFactory = null!;
    private SignalListItemFactory _sizeFactory = null!;
    private SignalListItemFactory _versionFactory = null!;
    private SignalListItemFactory _repositoryFactory = null!;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/Package/PackageWindow.ui"), -1);
        _overlay = (Overlay)builder.GetObject("PackageWindow")!;
        _columnView = (ColumnView)builder.GetObject("package_column_view")!;
        var checkColumn = (ColumnViewColumn)builder.GetObject("check_column")!;
        var nameColumn = (ColumnViewColumn)builder.GetObject("name_column")!;
        var sizeColumn = (ColumnViewColumn)builder.GetObject("size_column")!;
        var versionColumn = (ColumnViewColumn)builder.GetObject("version_column")!;
        var repositoryColumn = (ColumnViewColumn)builder.GetObject("repository_column")!;
        var installButton = (Button)builder.GetObject("install_button")!;
        var localInstallButton = (Button)builder.GetObject("install_local_button")!;
        var searchEntry = (SearchEntry)builder.GetObject("search_entry")!;
        _listStore = Gio.ListStore.New(AlpmPackageGObject.GetGType());
        _filter = CustomFilter.New(FilterPackage);
        _filterListModel = FilterListModel.New(_listStore, _filter);
        _selectionModel = SingleSelection.New(_filterListModel);
        _selectionModel.CanUnselect = true;
        _columnView.SetModel(_selectionModel);
        SetupColumns(checkColumn, nameColumn, sizeColumn, versionColumn, repositoryColumn);

        ColumnViewHelper.AlignColumnHeader(_columnView, 1, Align.End);
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
        searchEntry.OnSearchChanged += (_, _) =>
        {
            _searchText = searchEntry.GetText();
            ApplyFilter();
        };
        installButton.OnClicked += (_, _) => { _ = InstallSelectedAsync(); };
        localInstallButton.OnClicked += (_, _) => { _ = InstallLocalPackage(); };

        return _overlay;
    }

    private void SetupColumns(ColumnViewColumn checkColumn, ColumnViewColumn nameColumn,
        ColumnViewColumn sizeColumn, ColumnViewColumn versionColumn, ColumnViewColumn repositoryColumn)
    {
        _checkFactory = SignalListItemFactory.New();
        _checkFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var check = new CheckButton { MarginStart = 10, MarginEnd = 10 };
            listItem.SetChild(check);

            check.OnToggled += (s, _) =>
            {
                if (listItem.GetItem() is AlpmPackageGObject pkgObj)
                {
                    pkgObj.IsSelected = s.GetActive();
                }
            };
        };

        _checkFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmPackageGObject pkgObj ||
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
            // Unsubscribe to break the reference chain
            if (_checkBinding.Remove(listItem, out var handler))
                pkgObj.OnSelectionToggled -= handler;
        };

        checkColumn.SetFactory(_checkFactory);

        _nameFactory = SignalListItemFactory.New();
        _nameFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var box = Box.New(Orientation.Horizontal, 6);
            var label = Label.New(string.Empty);
            var installedIcon = Image.NewFromIconName("object-select-symbolic");
            
            box.Append(label);
            box.Append(installedIcon);
            listItem.SetChild(box);
        };
        _nameFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmPackageGObject pkgObj || pkgObj.Package == null ||
                listItem.GetChild() is not Box box) return;

            var label = (Label)box.GetFirstChild()!;
            var installedIcon = (Image)label.GetNextSibling()!;

            label.SetText(pkgObj.Package.Name);
            label.Halign = Align.Start;
            
            installedIcon.Visible = pkgObj.IsInstalled;
            installedIcon.TooltipText = "Installed";
        };
        nameColumn.SetFactory(_nameFactory);

        _sizeFactory = SignalListItemFactory.New();
        _sizeFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };
        _sizeFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(SizeHelpers.FormatSize(pkg.InstalledSize));
            label.Halign = Align.End;
        };
        sizeColumn.SetFactory(_sizeFactory);

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
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Version);
            label.Halign = Align.End;
        };
        versionColumn.SetFactory(_versionFactory);

        _repositoryFactory = new SignalListItemFactory();
        _repositoryFactory.OnSetup += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            var label = Label.New(string.Empty);
            listItem.SetChild(label);
        };

        _repositoryFactory.OnBind += (_, args) =>
        {
            var listItem = (ListItem)args.Object;
            if (listItem.GetItem() is not AlpmPackageGObject { Package: { } pkg } ||
                listItem.GetChild() is not Label label) return;
            label.SetText(pkg.Repository);
            label.Halign = Align.End;
        };
        repositoryColumn.SetFactory(_repositoryFactory);
    }


    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        // Disconnect the model from the view to break circular refs
        _columnView.SetModel(null);

        // Dispose all GObject items BEFORE removing them
        for (uint i = 0; i < _listStore.GetNItems(); i++)
        {
            if (_listStore.GetObject(i) is AlpmPackageGObject pkgObj)
            {
                pkgObj.Package = null;
                pkgObj.Dispose();
            }
        }

        _listStore.RemoveAll();

        _selectionModel.Dispose();
        _filterListModel.Dispose();
        _filter.Dispose();
        _listStore.Dispose();
        
        _checkBinding.Clear();
        _checkBinding = null!;
        
        _packages = null!;
        
        _columnView = null!;
        _overlay = null!;
        
        _checkFactory.Dispose();
        _nameFactory.Dispose();
        _sizeFactory.Dispose();
        _versionFactory.Dispose();
        _repositoryFactory.Dispose();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }

    private async Task LoadDataAsync(CancellationToken ct = default)
    {
        try
        {
            _packages = await privilegedOperationService.GetAvailablePackagesAsync();
            var installedPackages = await privilegedOperationService.GetInstalledPackagesAsync();
            var installedNames = new HashSet<string>(installedPackages?.Select(x => x.Name) ?? []);
            
            ct.ThrowIfCancellationRequested();
            var queue = new Queue<AlpmPackageDto>(_packages);

            GLib.Functions.IdleAdd(0, () =>
            {
                _listStore.RemoveAll();
                return false;
            });
            
            GLib.Functions.IdleAdd(0, () =>
            {
                _listStore.RemoveAll();
                return false;
            });

            GLib.Functions.IdleAdd(0, () =>
            {
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                // This number might need to be adjusted based on cpu. This comment is just so we can find this later
                // when we inevitably get a bug report about the package page being slow.
                const int batchSize = 1000;
                var count = 0;
                var batch = new List<AlpmPackageGObject>();
                while (queue.Count > 0 && count < batchSize)
                {
                    var dequeued = queue.Dequeue();
                    batch.Add(new AlpmPackageGObject() { Package = dequeued, IsInstalled = installedNames.Contains(dequeued.Name)});
                    count++;
                }

                // ReSharper disable once CoVariantArrayConversion
                _listStore.Splice(_listStore.GetNItems(), 0, batch.ToArray(), (uint)batch.Count);

                return queue.Count > 0;
            });
        }
        catch (OperationCanceledException)
        {
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

    private bool FilterPackage(GObject.Object obj)
    {
        if (obj is not AlpmPackageGObject pkgObj || pkgObj.Package == null)
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        return pkgObj.Package.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               pkgObj.Package.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task InstallSelectedAsync()
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
                await privilegedOperationService.InstallPackagesAsync(selectedPackages);
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
    
     private async Task InstallLocalPackage()
    {
        

        try
        {
            var dialog = FileDialog.New();
            dialog.SetTitle("Install Local Package");

            var filter = FileFilter.New();
            filter.SetName("Local package files (\"*.xz\", \"*.gz\", \"*.zst\")");
            filter.AddPattern("*.xz");
            filter.AddPattern("*.gz");
            filter.AddPattern("*.zst");

            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(filter);
            dialog.SetFilters(filters);

            var file = await dialog.OpenAsync((Window)_overlay.GetRoot()!);

            if (file is not null)
            {
                lockoutService.Show($"Installing local package...");
                var result = await privilegedOperationService.InstallLocalPackageAsync(file.GetPath()!);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install local package: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to install local package: {ex.Message}");
        }
        finally
        {
           lockoutService.Hide();
        }
    }

}