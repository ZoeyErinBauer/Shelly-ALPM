using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels.Flatpak;

public class FlatpakUpdateViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }

    private readonly IUnprivilegedOperationService _unprivilegedOperationService;

    private string? _searchText;
    private List<FlatpakModel> _availablePackages = [];

    public FlatpakUpdateViewModel(IScreen screen)
    {
        HostScreen = screen;

        _unprivilegedOperationService = App.Services.GetRequiredService<IUnprivilegedOperationService>();

        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        RefreshCommand = ReactiveCommand.Create(LoadData);
        
        UpgradeCommand = ReactiveCommand.CreateFromTask(UpdateAllCommand);

        UpdatePackageCommand = ReactiveCommand.CreateFromTask<FlatpakModel>(UpdateCommand);

        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            var result = await Task.Run(() => _unprivilegedOperationService.ListFlatpakUpdates());
            Console.WriteLine($@"[DEBUG_LOG] Loaded {result.Count} installed packages");

            var models = result.Select(u => new FlatpakModel
            {
                Name = u.Name,
                Version = u.Version,
                Summary = u.Summary,
                IconPath = $"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{u.Id}.png",
                Id = u.Id,
                Kind = u.Kind == 0
                    ? "App"
                    : "Runtime",
            }).ToList();
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _availablePackages = models;
                ApplyFilter();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($@"Failed to load installed packages for update: {e.Message}");
        }
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _availablePackages
            : _availablePackages.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Version.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        AvailablePackages.Clear();

        foreach (var package in filtered)
        {
            AvailablePackages.Add(package);
        }
    }

    private bool _showConfirmDialog;

    private bool ShowConfirmDialog
    {
        get => _showConfirmDialog;
        set => this.RaiseAndSetIfChanged(ref _showConfirmDialog, value);
    }

    public void ToggleConfirmAction()
    {
        ShowConfirmDialog = !ShowConfirmDialog;
    }

    public async Task UpdateCommand(FlatpakModel package)
    {
        var mainWindow = HostScreen as MainWindowViewModel;

        try
        {
            // Set busy
            if (mainWindow != null)
            {
                mainWindow.GlobalProgressValue = 0;
                mainWindow.GlobalBytesValue = "";
                mainWindow.GlobalProgressText = "0%";
                mainWindow.IsGlobalBusy = true;
                mainWindow.GlobalBusyMessage = "Updated selected package...";
            }

            //do work

            var result = await _unprivilegedOperationService.UpdateFlatpakPackage(package.Id);
            if (!result.Success)
            {
                Console.WriteLine($@"Failed to remove packages: {result.Error}");
            }

            LoadData();
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

    private async Task UpdateAllCommand()
    {
        var mainWindow = HostScreen as MainWindowViewModel;

        if (_availablePackages.Count == 0) return;
        
        try
        {
            // Set busy
            if (mainWindow != null)
            {
                mainWindow.GlobalProgressValue = 0;
                mainWindow.GlobalProgressText = "0%";
                mainWindow.GlobalBytesValue = "";
                mainWindow.IsGlobalBusy = true;
                mainWindow.GlobalBusyMessage = "Updated selected package...";
            }

            //do work

            var result = await _unprivilegedOperationService.FlatpakUpgrade();
            if (!result.Success)
            {
                Console.WriteLine($@"Failed to update packages: {result.Error}");
            }

            LoadData();
        }
        finally
        {
            //always exit globally busy in case of failure
            mainWindow?.IsGlobalBusy = false;
        }
    }
    
    public string UrlPathSegment { get; } = Guid.NewGuid().ToString()[..5];

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<FlatpakModel, Unit> UpdatePackageCommand { get; set; }
    public ReactiveCommand<Unit, Unit> UpgradeCommand { get; set; }
    
    public ObservableCollection<FlatpakModel> AvailablePackages { get; set; } = [];


    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AvailablePackages.Clear();
            _availablePackages.Clear();
        }

        base.Dispose(disposing);
    }
}