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
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels.AUR;

public class AurUpdateViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private IAurPackageManager _aurPackageManager = new AurPackageManager();
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<UpdateModel>> _filteredPackages;
    private readonly ICredentialManager _credentialManager;


    public AurUpdateViewModel(IScreen screen, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;
        _privilegedOperationService = privilegedOperationService;
        PackagesForUpdating = new ObservableCollection<UpdateModel>();
        _credentialManager = credentialManager;

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.PackagesForUpdating.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        AlpmUpdateCommand = ReactiveCommand.CreateFromTask(AlpmUpdate);
        SyncCommand = ReactiveCommand.CreateFromTask(Sync);
        TogglePackageCheckCommand = ReactiveCommand.Create<UpdateModel>(TogglePackageCheck);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        LoadData();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }

    private async Task Sync()
    {
        try
        {
            PackagesForUpdating.Clear();
            await LoadData();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to sync packages for update: {e.Message}");
        }
    }

    private async Task AlpmUpdate()
    {
        var selectedPackages = PackagesForUpdating.Where(x => x.IsChecked).Select(x => x.Name).ToList();
        if (selectedPackages.Any())
        {
            MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

            try
            {
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
                    mainWindow.GlobalBusyMessage = "Updating selected packages...";
                }

                //do work
                var result = await _privilegedOperationService.UpdateAurPackagesAsync(selectedPackages);
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

    private async Task LoadData()
    {
        try
        {
            await Task.Run(() => _aurPackageManager.Initialize());
            var updates = await Task.Run(() => _aurPackageManager.GetPackagesNeedingUpdate());

            var models = updates.Select(u => new UpdateModel
            {
                Name = u.Name,
                CurrentVersion = u.Version,
                NewVersion = u.NewVersion,
                DownloadSize = u.DownloadSize,
                IsChecked = false
            }).ToList();

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var model in models)
                {
                    PackagesForUpdating.Add(model);
                }

                this.RaisePropertyChanged(nameof(PackagesForUpdating));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load package updates: {e.Message}");
        }
    }

    private IEnumerable<UpdateModel> Search(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return PackagesForUpdating;
        }

        return PackagesForUpdating.Where(p =>
            p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
    
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AlpmUpdateCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SyncCommand { get; }

    public IEnumerable<UpdateModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public void CheckAll()
    {
        var targetState = PackagesForUpdating.Any(x => !x.IsChecked);

        foreach (var item in PackagesForUpdating)
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
}