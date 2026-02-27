using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.LocalDatabase;

namespace Shelly_UI.ViewModels.Flatpak;

public class FlatpakInstallViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }

    private readonly IUnprivilegedOperationService _unprivilegedOperationService;

    private string? _searchText;

   private readonly IDatabaseService _databaseService;
    public ObservableCollection<FlatpakModel> Flatpaks { get; set; } = [];
    private int _currentPage;
    private bool _isLoading;
    
    public ReactiveCommand<Unit, Unit> LoadMoreCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }


    public FlatpakInstallViewModel(IScreen screen)
    {
        HostScreen = screen;

        _unprivilegedOperationService = App.Services.GetRequiredService<IUnprivilegedOperationService>();
        _databaseService = App.Services.GetRequiredService<IDatabaseService>();
        
        LoadMoreCommand = ReactiveCommand.CreateFromTask(LoadMoreAsync);
        SearchCommand = ReactiveCommand.CreateFromTask(PerformSearchAsync);

        InstallPackagesCommand = ReactiveCommand.CreateFromTask<FlatpakModel>(InstallPackage);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);

        this.WhenAnyValue(x => x.CategoryEnum)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => PerformSearchAsync());

        if (_databaseService.CollectionExists<FlatpakModel>("flatpaks")) return;
        _ = Refresh();
        _databaseService.EnsureIndex<FlatpakModel>("collectionName", x => x.Name, x => x.Categories);
        LoadingData = true;
    }

    /// <summary>
    /// Updates the Database with the most recent information in the local reference file.
    /// </summary>
    private async Task Refresh()
    {
        try
        {
            await _unprivilegedOperationService.FlatpakSyncRemoteAppstream();
            var available = await _unprivilegedOperationService.ListAppstreamFlatpak();

            var models = available.Select(u => new FlatpakModel
            {
                Name = u.Name,
                Version = u.Releases[0].Version.ToString(),
                Summary = u.Summary,
                Description = u.Description,
                IconPath = $"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{u.Id}.png",
                Id = u.Id,
                Categories = u.Categories,
                Kind = u.Kind == 0
                    ? "App"
                    : "Runtime",
            }).ToList();
            await new DatabaseService().AddToDatabase(models.ToList(),"flatpaks");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to refresh installed packages: {e.Message}");
        }

        if (LoadingData)
        {
            await LoadInitialDataAsync();
            LoadingData = false;
        }
    }

    private Task PerformSearchAsync()
    {
        Flatpaks.Clear();
        _currentPage = 0;

        var items = GetNextPage(CategoryEnum != Enums.FlatpakCategories.None ? CategoryEnum.ToString() : null);

        foreach (var item in items)
        {
            Flatpaks.Add(item);
        }

        return Task.CompletedTask;
    }

    private Task LoadInitialDataAsync()
    {
        Flatpaks.Clear();
        _currentPage = 0;

        var items = GetNextPage(CategoryEnum != Enums.FlatpakCategories.None ? CategoryEnum.ToString() : null);

        foreach (var item in items)
        {
            Flatpaks.Add(item);
        }

        return Task.CompletedTask;
    }

    private Task LoadMoreAsync()
    {
        if (_isLoading) return Task.CompletedTask;

        _isLoading = true;
        _currentPage++;
        try
        {
            var items = GetNextPage(CategoryEnum != Enums.FlatpakCategories.None ? CategoryEnum.ToString() : null);

            if (items.Count != 0)
            {
                foreach (var item in items)
                {
                    Flatpaks.Add(item);
                }
            }
        }
        finally
        {
            _isLoading = false;
        }

        return Task.CompletedTask;
    }

    private List<FlatpakModel> GetNextPage(string? category) => _databaseService.GetNextPage<FlatpakModel, string>(
        "flatpaks",
        _currentPage,
        20,
        x => x.Name,
        x => (string.IsNullOrWhiteSpace(SearchText) ||
              x.Name.Contains(SearchText) ||
              x.Summary.Contains(SearchText)) &&
             (string.IsNullOrWhiteSpace(category) ||
              x.Categories.Contains(category))
    );

    public IEnumerable<FlatpakCategories> FlatpakCategories { get; } =
        Enum.GetValues<FlatpakCategories>();

    private FlatpakCategories _categoryEnum;

    public FlatpakCategories CategoryEnum
    {
        get => _categoryEnum;
        set { this.RaiseAndSetIfChanged(ref _categoryEnum, value); }
    }

    private bool _loadingData;

    public bool LoadingData
    {
        get => _loadingData;
        set => this.RaiseAndSetIfChanged(ref _loadingData, value);
    }


    private async Task InstallPackage(FlatpakModel package)
    {
        var mainWindow = HostScreen as MainWindowViewModel;

        try
        {
            // Set busy
            if (mainWindow != null)
            {
                mainWindow.GlobalProgressValue = 0;
                mainWindow.GlobalProgressText = "0%";
                mainWindow.IsGlobalBusy = true;
                mainWindow.GlobalBytesValue = "";
                mainWindow.GlobalBusyMessage = "Installing package...";
            }

            //do work

            var result = await _unprivilegedOperationService.InstallFlatpakPackage(package.Id);
            if (!result.Success)
            {
                Console.WriteLine($"Failed to remove packages: {result.Error}");
            }
        }
        finally
        {
            //always exit globally busy in case of failure
            mainWindow?.IsGlobalBusy = false;
        }
    }


    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);
    
    public ReactiveCommand<FlatpakModel, Unit> InstallPackagesCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    
    
    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flatpaks.Clear();
        }

        base.Dispose(disposing);
    }
}