using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using Shelly_UI.Assets;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Views;

namespace Shelly_UI.ViewModels;

public class UpdateViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    private readonly ICredentialManager _credentialManager;
    private readonly IPrivilegedOperationService _privilegedOperationService;

    private List<UpdateModel> _allPackagesForUpdate = [];
    private string? _searchText;

    public UpdateViewModel(IScreen screen, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;
        _privilegedOperationService = privilegedOperationService;
        PackagesForUpdating = [];
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

    public ReactiveCommand<Unit, Unit> AlpmUpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public ObservableCollection<UpdateModel> PackagesForUpdating { get; set; }

    public ReactiveCommand<UpdateModel, Unit> TogglePackageCheckCommand { get; }
    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString()[..5];

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allPackagesForUpdate
            : _allPackagesForUpdate.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        PackagesForUpdating.Clear();

        foreach (var package in filtered) PackagesForUpdating.Add(package);
    }

    private async Task Sync()
    {
        try
        {
            var result = await _privilegedOperationService.SyncDatabasesAsync();
            if (!result.Success) Console.WriteLine($"Failed to sync databases: {result.Error}");

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


        var hasUncheckedPackages = _allPackagesForUpdate.Any(x => !x.IsChecked);

        if (hasUncheckedPackages)
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: not null
                } desktop)
            {
                var dialog =
                    new QuestionDialog(
                        Resources.AlpmUpdatePromptDialog,
                        Resources.Continue, Resources.Cancel);
                var result = await dialog.ShowDialog<bool>(desktop.MainWindow);

                if (!result) return; // User cancelled
            }

        if (selectedPackages.Count != 0)
        {
            var mainWindow = HostScreen as MainWindowViewModel;

            try
            {
                // Request credentials 
                if (!_credentialManager.IsValidated || _credentialManager.IsExpired())
                {
                    if (!await _credentialManager.RequestCredentialsAsync(Resources.UpdatePackages)) return;

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
                    mainWindow.GlobalBytesValue = "";
                    mainWindow.IsGlobalBusy = true;
                    mainWindow.GlobalBusyMessage = isFullUpgrade
                        ? Resources.PerformingFullSystemUpgrade
                        : Resources.UpdatingSelectedPackages;
                }

                // Use full system upgrade when all packages are selected, otherwise update specific packages
                OperationResult result;
                if (isFullUpgrade)
                    result = await _privilegedOperationService.UpgradeSystemAsync();
                else
                    result = await _privilegedOperationService.UpdatePackagesAsync(selectedPackages);

                if (!result.Success)
                {
                    Console.WriteLine($"Failed to update packages: {result.Error}");
                    var err = Logs.FirstOrDefault(x => x.Contains("[ALPM_ERROR]"));
                    mainWindow?.ShowToast($"Update failed: {err}", false);
                }
                else
                {
                    var packageCount = selectedPackages.Count;
                    mainWindow?.ShowToast(
                        string.Format(Resources.GenericPackageUpdateSuccess, packageCount));
                }
                

                await Sync();
            }
            finally
            {
                //always exit globally busy in case of failure
                mainWindow?.IsGlobalBusy = false;
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
                IsChecked = true
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

    public void CheckAll()
    {
        var targetState = _allPackagesForUpdate.Any(x => !x.IsChecked);

        foreach (var item in _allPackagesForUpdate) item.IsChecked = targetState;
    }

    private static void TogglePackageCheck(UpdateModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }

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