using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels.Flatpak;

public class FlatpakRemoveViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }

    private readonly IUnprivilegedOperationService _unprivilegedOperationService;

    private List<FlatpakModel> _avaliablePackages = new();

    private string? _searchText;

    public FlatpakRemoveViewModel(IScreen screen)
    {
        HostScreen = screen;

        _unprivilegedOperationService = App.Services.GetRequiredService<IUnprivilegedOperationService>();
        AvailablePackages = new ObservableCollection<FlatpakModel>();

        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        RefreshCommand = ReactiveCommand.Create(LoadData);
        RemovePackageCommand = ReactiveCommand.CreateFromTask<FlatpakModel>(RemovePackage);

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

    private async void LoadData()
    {
        _avaliablePackages.Clear();
        AvailablePackages.Clear();
        try
        {
            var result = await Task.Run(() => _unprivilegedOperationService.ListFlatpakPackages());

            var models = result.Select(u => new FlatpakModel
            {
                Name = u.Name,
                Version = u.Version,
                Id = u.Id,
                IconPath = $"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{u.Id}.png",
                Kind = u.Kind == 0
                    ? "App"
                    : "Runtime",
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

    public async Task RemovePackage(FlatpakModel package)
    {
        MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

        try
        {
            /*// Set busy
            if (mainWindow != null)
            {
                mainWindow.GlobalProgressValue = 0;
                mainWindow.GlobalProgressText = "0%";
                mainWindow.IsGlobalBusy = true;
                mainWindow.GlobalBusyMessage = "Removing selected package...";
            }*/

            //do work

            var result = await _unprivilegedOperationService.RemoveFlatpakPackage(package.Id);
            if (!result.Success)
            {
                Console.WriteLine($"Failed to remove packages: {result.Error}");
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

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public System.Reactive.Unit Unit => System.Reactive.Unit.Default;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }
    public ReactiveCommand<FlatpakModel, Unit> RemovePackageCommand { get; }

    public ObservableCollection<FlatpakModel> AvailablePackages { get; set; }


    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

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