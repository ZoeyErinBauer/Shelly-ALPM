using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class PackageViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel, IActivatableViewModel
{
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new ViewModelActivator();
    private IAlpmManager _alpmManager = AlpmService.Instance;
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<PackageModel>> _filteredPackages;

    private readonly ConfigService _configService = new();

    private IAppCache _appCache;
    private readonly ICredentialManager _credentialManager;

    public PackageViewModel(IScreen screen, IAppCache appCache, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;
        AvaliablePackages = new ObservableCollection<PackageModel>();

        _appCache = appCache;
        _privilegedOperationService = privilegedOperationService;
        _credentialManager = credentialManager;

        // Always initialize ConsoleLogService to ensure stderr interception is active
        // This is needed even when console is disabled so that logs from CLI are captured
        var _ = ConsoleLogService.Instance;

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.AvaliablePackages.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        AlpmInstallCommand = ReactiveCommand.CreateFromTask(AlpmInstall);
        SyncCommand = ReactiveCommand.CreateFromTask(Sync);
        TogglePackageCheckCommand = ReactiveCommand.Create<PackageModel>(TogglePackageCheck);

        // Load data when the view model is activated (navigated to)
        LoadData();
    }


    private async Task Sync()
    {
        try
        {
            var result = await _privilegedOperationService.SyncDatabasesAsync();
            if (!result.Success)
            {
                Console.Error.WriteLine($"Failed to sync databases: {result.Error}");
            }

            // Re-initialize the local alpm manager to pick up synced data
            await Task.Run(() => _alpmManager.Initialize());
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
            Console.Error.WriteLine($"Failed to sync packages: {e.Message}");
        }
    }

    private async void LoadData()
    {
        try
        {
            await Task.Run(() => _alpmManager.Initialize());
            var packages = await Task.Run(() => _alpmManager.GetAvailablePackages());

            var installed = await Task.Run(() => _alpmManager.GetInstalledPackages());   
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
            MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

            try
            {
                ShowConfirmDialog = false;

                if (!_credentialManager.IsValidated)
                {
                    if (!await _credentialManager.RequestCredentialsAsync("Install Packages")) return;

                    if (string.IsNullOrEmpty(_credentialManager.GetPassword())) return;

                    var isValidated = await _credentialManager.ValidateInputCredentials();

                    if (!isValidated) return;
                }


                // Set busy
                if (mainWindow != null)
                {
                    mainWindow.GlobalProgressValue = 0;
                    mainWindow.GlobalProgressText = "0%";
                    mainWindow.IsGlobalBusy = true;
                    mainWindow.GlobalBusyMessage = "Installing selected packages...";
                }

                //do work
                var result = await _privilegedOperationService.InstallPackagesAsync(selectedPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install packages: {result.Error}");
                }

                await Sync();
            }
            finally
            {
                //always exit globally busy in case of failure
                if (mainWindow != null)
                {
                    mainWindow.IsGlobalBusy = false;
                }
            }
        }
        else
        {
            ShowConfirmDialog = false;
        }
    }

    private void TogglePackageCheck(PackageModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
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