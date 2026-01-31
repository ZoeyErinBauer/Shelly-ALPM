using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels;

public class UpdateViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private string? _searchText;
    private readonly ICredentialManager _credentialManager;
    
    private List<UpdateModel> _allPackagesForUpdate = new();

    public UpdateViewModel(IScreen screen, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;
        _privilegedOperationService = privilegedOperationService;
        PackagesForUpdating = new ObservableCollection<UpdateModel>();
        _credentialManager = credentialManager;
        
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        AlpmUpdateCommand = ReactiveCommand.CreateFromTask(AlpmUpdate);
        SyncCommand = ReactiveCommand.CreateFromTask(Sync);
        TogglePackageCheckCommand = ReactiveCommand.Create<UpdateModel>(TogglePackageCheck);

        LoadData();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allPackagesForUpdate
            : _allPackagesForUpdate.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        PackagesForUpdating.Clear();
        
        foreach (var package in filtered)
        {
            PackagesForUpdating.Add(package);
        }
    }

    private async Task Sync()
    {
        try
        {
            var result = await _privilegedOperationService.SyncDatabasesAsync();
            if (!result.Success)
            {
                Console.WriteLine($"Failed to sync databases: {result.Error}");
            }

            // Reload data via CLI after sync
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _allPackagesForUpdate.Clear();
                PackagesForUpdating.Clear();
                LoadData();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to sync packages for update: {e.Message}");
        }
    }

    private async Task AlpmUpdate()
    {
        var selectedPackages = _allPackagesForUpdate.Where(x => x.IsChecked).Select(x => x.Name).ToList();
        if (selectedPackages.Any())
        {
            MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

            try
            {
                // Request credentials 
                if (!_credentialManager.IsValidated)
                {
                    if (!await _credentialManager.RequestCredentialsAsync("Update Packages")) return;

                    if (string.IsNullOrEmpty(_credentialManager.GetPassword())) return;

                    var isValidated = await _credentialManager.ValidateInputCredentials();

                    if (!isValidated) return;
                }

                // Determine if this is a full system upgrade or selective update
                var isFullUpgrade = selectedPackages.Count == _allPackagesForUpdate.Count;

                // Set busy
                if (mainWindow != null)
                {
                    mainWindow.GlobalProgressValue = 0;
                    mainWindow.GlobalProgressText = "0%";
                    mainWindow.IsGlobalBusy = true;
                    mainWindow.GlobalBusyMessage = isFullUpgrade 
                        ? "Performing full system upgrade..." 
                        : "Updating selected packages...";
                }

                // Use full system upgrade when all packages are selected, otherwise update specific packages
                OperationResult result;
                if (isFullUpgrade)
                {
                    result = await _privilegedOperationService.UpgradeSystemAsync();
                }
                else
                {
                    result = await _privilegedOperationService.UpdatePackagesAsync(selectedPackages);
                }
                
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to update packages: {result.Error}");
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
    }

    private async void LoadData()
    {
        try
        {
            // Use CLI via PrivilegedOperationService to get packages needing update
            var updates = await _privilegedOperationService.GetPackagesNeedingUpdateAsync();

            var models = updates.Select(u => new UpdateModel
            {
                Name = u.Name,
                CurrentVersion = u.CurrentVersion,
                NewVersion = u.NewVersion,
                DownloadSize = u.DownloadSize,
                IsChecked = false
            }).ToList();

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _allPackagesForUpdate = models;
                ApplyFilter();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load package updates: {e.Message}");
        }
    }

    public ReactiveCommand<Unit, Unit> AlpmUpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public void CheckAll()
    {
        var targetState = _allPackagesForUpdate.Any(x => !x.IsChecked);

        foreach (var item in _allPackagesForUpdate)
        {
            item.IsChecked = targetState;
        }
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ObservableCollection<UpdateModel> PackagesForUpdating { get; set; }

    private void TogglePackageCheck(UpdateModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }

    public ReactiveCommand<UpdateModel, Unit> TogglePackageCheckCommand { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PackagesForUpdating?.Clear();
            _allPackagesForUpdate?.Clear();
        }
        base.Dispose(disposing);
    }
}