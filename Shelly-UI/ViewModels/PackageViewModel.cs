using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class PackageViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private IAlpmManager _alpmManager = AlpmService.Instance;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<PackageModel>> _filteredPackages;

    private IAppCache _appCache;

    private readonly ObservableAsPropertyHelper<string> _fullLogText;
    public string FullLogText => _fullLogText.Value;
    
    public PackageViewModel(IScreen screen, IAppCache appCache)
    {
        HostScreen = screen;
        AvaliablePackages = new ObservableCollection<PackageModel>();

        
        
        _appCache = appCache;

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.AvaliablePackages.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        _fullLogText = ConsoleLogService.Instance.Logs
            .ToObservableChangeSet() 
            .QueryWhenChanged(items => string.Join(Environment.NewLine, items))
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, x => x.FullLogText);
        
        AlpmInstallCommand = ReactiveCommand.CreateFromTask(AlpmInstall);
        SyncCommand = ReactiveCommand.CreateFromTask(Sync);
        TogglePackageCheckCommand = ReactiveCommand.Create<PackageModel>(TogglePackageCheck);

        // In a real app, you'd likely resolve this via DI
        
        LoadData();
    }

    private async Task Sync()
    {
        try
        {
            await Task.Run(() => _alpmManager.IntializeWithSync());
            // Clear cache and reload data by storing null
            await _appCache.StoreAsync<List<PackageModel>?>(nameof(CacheEnums.PackageCache), null);
            await _appCache.StoreAsync(nameof(CacheEnums.InstalledCache), _alpmManager.GetInstalledPackages());

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                AvaliablePackages.Clear();
                LoadData();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to sync packages: {e.Message}");
        }
    }

    private async void LoadData()
    {
        var cachedPackages = await _appCache.GetAsync<List<PackageModel>?>(nameof(CacheEnums.PackageCache));

        try
        {
            await Task.Run(() => _alpmManager.Initialize());
            var packages = await Task.Run(() => _alpmManager.GetAvailablePackages());

            var installed = await _appCache.GetAsync<List<AlpmPackageDto>?>(nameof(CacheEnums.InstalledCache));
            var installedNames = new HashSet<string>(installed?.Select(x => x.Name) ?? Enumerable.Empty<string>());

            var models = packages.Select(u => new PackageModel
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
                Description = u.Description,
                Url = u.Url,
                IsChecked = false,
                IsInstalled = installedNames.Contains(u.Name),
                Repository = u.Repository
            }).ToList();

            await _appCache.StoreAsync(nameof(CacheEnums.PackageCache), models);

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var model in models)
                {
                    AvaliablePackages.Add(model);
                }

                this.RaisePropertyChanged(nameof(AvaliablePackages));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load available packages: {e.Message}");
        }
    }

    private IEnumerable<PackageModel> Search(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return AvaliablePackages;
        }

        return AvaliablePackages.Where(p =>
            p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private bool _showConfirmDialog;

    public bool ShowConfirmDialog
    {
        get => _showConfirmDialog;
        set => this.RaiseAndSetIfChanged(ref _showConfirmDialog, value);
    }

    public void ToggleConfirmAction()
    {
        ShowConfirmDialog = !ShowConfirmDialog;
    }

    private async Task AlpmInstall()
    {
        var selectedPackages = AvaliablePackages.Where(x => x.IsChecked).Select(x => x.Name).ToList();
        if (selectedPackages.Any())
        {
            ShowConfirmDialog = false;
            await Task.Run(() => _alpmManager.InstallPackages(selectedPackages));
            await Sync();
        }
        else
        {
            ShowConfirmDialog = false;
        }
    }

    private void TogglePackageCheck(PackageModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }
    
    private bool _isBottomPanelCollapsed = true;
    public bool IsBottomPanelCollapsed
    {
        get => _isBottomPanelCollapsed;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelCollapsed, value);
    }

    public void ToggleBottomPanel()
    {
        IsBottomPanelCollapsed = !IsBottomPanelCollapsed;
    }

    public ReactiveCommand<PackageModel, Unit> TogglePackageCheckCommand { get; }

    public ReactiveCommand<Unit, Unit> AlpmInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }

    public ObservableCollection<PackageModel> AvaliablePackages { get; set; }

    public IEnumerable<PackageModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);
}