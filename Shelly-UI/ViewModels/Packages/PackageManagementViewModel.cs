using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Shelly_UI.Assets;
using Shelly_UI.BaseClasses;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class PackageManagementViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly IAppCache _appCache;
    private string? _searchText;
    private readonly ICredentialManager _credentialManager;
    
    private List<PackageModel> _avaliablePackages = new();

    public PackageManagementViewModel(IScreen screen, IAppCache appCache, IPrivilegedOperationService privilegedOperationService, ICredentialManager credentialManager)
    {
        HostScreen = screen;
        _appCache = appCache;
        _privilegedOperationService = privilegedOperationService;
        AvailablePackages = new ObservableCollection<PackageModel>();
        _credentialManager = credentialManager;
        
        // When search text changes, update the observable collection
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        RemovePackagesCommand = ReactiveCommand.CreateFromTask(RemovePackages);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);
        TogglePackageCheckCommand = ReactiveCommand.Create<PackageModel>(TogglePackageCheck);
        
        LoadData();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _avaliablePackages
            : _avaliablePackages.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Version.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        AvailablePackages.Clear();
        
        foreach (var package in filtered)
        {
            AvailablePackages.Add(package);
        }
    }

    private async Task Refresh()
    {
        try
        {
            var result = await _privilegedOperationService.SyncDatabasesAsync();
            if (!result.Success)
            {
                Console.Error.WriteLine($"Failed to sync databases: {result.Error}");
            }
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _avaliablePackages.Clear();
                AvailablePackages.Clear();
                LoadData();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to refresh installed packages: {e.Message}");
        }
    }

    private async void LoadData()
    {
        try
        {
            var packages = await _privilegedOperationService.GetInstalledPackagesAsync();
            var models = packages.Select(u => new PackageModel
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
                IsChecked = false
            }).ToList();
            
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _avaliablePackages = models;
                ApplyFilter();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load installed packages for removal: {e.Message}");
        }
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

    private async Task RemovePackages()
    {
        var selectedPackages = AvailablePackages.Where(x => x.IsChecked).Select(x => x.Name).ToList();
        if (selectedPackages.Any())
        {
            
            MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

            try
            {
                ShowConfirmDialog = false;
                // Request credentials 
                if (!_credentialManager.IsValidated || _credentialManager.IsExpired())
                {
                    if (!await _credentialManager.RequestCredentialsAsync("Remove Packages")) return;

                    if (string.IsNullOrEmpty(_credentialManager.GetPassword())) return;

                    var isValidated = await _credentialManager.ValidateInputCredentials();

                    if (!isValidated) return;
                }
                
                // Set busy
                if (mainWindow != null)
                {
                    mainWindow.GlobalProgressValue = 0;
                    mainWindow.GlobalBytesValue = "";
                    mainWindow.GlobalProgressText = "0%";
                    mainWindow.IsGlobalBusy = true;
                    mainWindow.GlobalBusyMessage = Resources.RemovingSelectedPackages;
                }

                //do work

                var result = await _privilegedOperationService.RemovePackagesAsync(selectedPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to remove packages: {result.Error}");
                    var err = Logs.FirstOrDefault(x => x.Contains("[ALPM_ERROR]"));
                    mainWindow?.ShowToast(string.Format(Resources.PackageRemovalFailed, err), isSuccess: false);
                }
                else
                {
                    // Update the installed packages cache after successful removal
                    var installedPackages = await _privilegedOperationService.GetInstalledPackagesAsync();
                    await _appCache.StoreAsync(nameof(CacheEnums.InstalledCache), installedPackages);
                    var packageCount = selectedPackages.Count;
                    mainWindow?.ShowToast(string.Format(Resources.PackageRemovalSuccess, packageCount));
                }

                await Refresh();
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

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public System.Reactive.Unit Unit => System.Reactive.Unit.Default;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RemovePackagesCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }

    public ObservableCollection<PackageModel> AvailablePackages { get; set; }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    private void TogglePackageCheck(PackageModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }
    
    public ReactiveCommand<PackageModel, Unit> TogglePackageCheckCommand { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AvailablePackages?.Clear();
            _avaliablePackages?.Clear();
        }
        base.Dispose(disposing);
    }
}