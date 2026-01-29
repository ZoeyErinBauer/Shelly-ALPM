using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PackageManager.Alpm;
using PackageManager.Aur;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels.AUR;

public class AurRemoveViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private IAurPackageManager _aurManager = new AurPackageManager();
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly IAppCache _appCache;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<AurModel>> _filteredPackages;
    private readonly ICredentialManager _credentialManager;

    public AurRemoveViewModel(IScreen screen, IAppCache appCache, IPrivilegedOperationService privilegedOperationService, ICredentialManager credentialManager)
    {
        HostScreen = screen;
        _appCache = appCache;
        _privilegedOperationService = privilegedOperationService;
        AvailablePackages = new ObservableCollection<AurModel>();
        _credentialManager = credentialManager;
        
        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.AvailablePackages.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        RemovePackagesCommand = ReactiveCommand.CreateFromTask(RemovePackages);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);
        TogglePackageCheckCommand = ReactiveCommand.Create<AurModel>(TogglePackageCheck);
        
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        LoadData();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }

    private async Task Refresh()
    {
        try
        {
            AvailablePackages.Clear();
            await LoadData();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to refresh installed packages: {e.Message}");
        }
    }

    private async Task LoadData()
    {
        try
        {
            await Task.Run(() => _aurManager.Initialize());
            var packages = await Task.Run(() => _aurManager.GetInstalledPackages());
            Console.WriteLine($@"[DEBUG_LOG] Loaded {packages.Count} installed packages");
            var models = packages.Select(u => new AurModel
            {
                Name = u.Name,
                Version = u.Version,
                IsChecked = false
            }).ToList();
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var pkg in models)
                {
                    AvailablePackages.Add(pkg);
                }

                this.RaisePropertyChanged(nameof(AvailablePackages));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($@"Failed to load installed packages for removal: {e.Message}");
        }
    }

    private IEnumerable<AurModel> Search(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return AvailablePackages;
        }

        return AvailablePackages.Where(p =>
            p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            p.Version.Contains(searchText, StringComparison.OrdinalIgnoreCase));
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
                    mainWindow.GlobalBusyMessage = "Removing selected packages...";
                }

                //do work
                var result = await _privilegedOperationService.RemoveAurPackagesAsync(selectedPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to remove packages: {result.Error}");
                }
                else
                {
                    // Update the installed packages cache after successful removal
                    await _appCache.StoreAsync(nameof(CacheEnums.InstalledCache), _aurManager.GetInstalledPackages());
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

    public ObservableCollection<AurModel> AvailablePackages { get; set; }

    public IEnumerable<AurModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    private void TogglePackageCheck(AurModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }
    
    public ReactiveCommand<AurModel, Unit> TogglePackageCheckCommand { get; }
}