using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.LocalDatabase;

namespace Shelly_UI.ViewModels;

public class MetaSearchViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public string? UrlPathSegment { get; } = Guid.NewGuid().ToString()[..5];
    public IScreen HostScreen { get; }

    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly ICredentialManager _credentialManager;
    private readonly IUnprivilegedOperationService _unprivilegedOperationService;
    private readonly IDatabaseService _databaseService;
    private readonly IConfigService _configService;

    private List<PackageModel> _allPackages = new();

    private string? _searchText;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    private ObservableCollection<MetaPackageModel> _availablePackages = new();

    public ObservableCollection<MetaPackageModel> AvailablePackages
    {
        get => _availablePackages;
        set => this.RaiseAndSetIfChanged(ref _availablePackages, value);
    }

    public MetaSearchViewModel(IScreen screen)
    {
        HostScreen = screen;
        _privilegedOperationService = App.Services.GetRequiredService<IPrivilegedOperationService>();
        _credentialManager = App.Services.GetRequiredService<ICredentialManager>();
        _unprivilegedOperationService = App.Services.GetRequiredService<IUnprivilegedOperationService>();
        _databaseService = App.Services.GetRequiredService<IDatabaseService>();
        _configService = App.Services.GetRequiredService<IConfigService>();
        LoadData();

        this.WhenAnyValue(x => x.SearchText)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(400))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => LoadData());
    }

    private async void LoadData()
    {
        try
        {
            if (!_databaseService.CollectionExists<FlatpakModel>("flatpaks"))
            {
                await Refresh();
            }

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                AvailablePackages = new ObservableCollection<MetaPackageModel>();
            });
            List<Task<List<MetaPackageModel>>> groupList = [];

            var standardTask = Task.Run(async () =>
            {
                var standardInstalled = await _privilegedOperationService.GetInstalledPackagesAsync().ContinueWith(x =>
                    x.Result.Select(y => new MetaPackageModel(y.Name, y.Name, y.Version, y.Description,
                        PackageType.ALPM, y.Description, y.Repository, true)).ToList());
                var standardAvailable = await _privilegedOperationService.SearchPackagesAsync(SearchText ?? "")
                    .ContinueWith(x =>
                        x.Result.Select(y => new MetaPackageModel(y.Name, y.Name, y.Version, y.Description,
                            PackageType.ALPM, y.Description, y.Repository,
                            standardInstalled.Any(z => z.Name == y.Name))).ToList());
                return standardAvailable;
            });
            groupList.Add(standardTask);

            Task<List<MetaPackageModel>>? flatpakGroup = null;
            if (_configService.LoadConfig().FlatPackEnabled)
            {
                flatpakGroup = Task.Run(async () =>
                {
                    var flatPakInstalled = await _unprivilegedOperationService.ListFlatpakPackages().ContinueWith(x =>
                        x.Result.Select(y => new MetaPackageModel(y.Id, y.Name, y.Version, y.Description,
                            PackageType.FLATPAK, y.Summary, "Flathub", true)).ToList());
                    var flatPakAvailable = _databaseService.GetCollection<FlatpakModel>("flatpaks")
                        .Where(x => x.Name.Contains(SearchText ?? "")).Select(y =>
                            new MetaPackageModel(y.Id, y.Name, y.Version, y.Description, PackageType.FLATPAK, y.Summary,
                                "Flathub", flatPakInstalled.Any(z => z.Name == y.Name))).ToList();
                    return flatPakAvailable;
                });
                groupList.Add(flatpakGroup);
            }

            Task<List<MetaPackageModel>>? aurGroup = null;
            if (_configService.LoadConfig().AurEnabled)
            {
                aurGroup = Task.Run(async () =>
                {
                    var aurInstalled = await _privilegedOperationService.GetAurInstalledPackagesAsync()
                        .ContinueWith(x =>
                            x.Result.Select(y => new MetaPackageModel(y.Name, y.Name, y.Version, y.Description ?? "",
                                PackageType.AUR, y.Url ?? "", "AUR", true)).ToList());
                    var aurAvailable = await _privilegedOperationService.SearchAurPackagesAsync(SearchText ?? "")
                        .ContinueWith(x => x.Result.Select(y =>
                            new MetaPackageModel(y.Name, y.Name, y.Version, y.Description ?? "", PackageType.AUR,
                                y.Url ?? "", "AUR", aurInstalled.Any(z => z.Name == y.Name))).ToList());
                    return aurAvailable;
                });
                groupList.Add(aurGroup);
            }

            List<MetaPackageModel> models = [];
            await foreach (var completedTask in Task.WhenEach(groupList))
            {
                var metaEnumerable = await completedTask;
                if (metaEnumerable.Count != 0)
                {
                    models.AddRange(metaEnumerable.ToList());
                }
            }

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                AvailablePackages = new ObservableCollection<MetaPackageModel>(models);
            });
        }

        catch (Exception e)
        {
            Console.WriteLine($"Failed to load packages: {e.Message}");
        }
    }

    private async Task Refresh()
    {
        try
        {
            await _databaseService.EnsureIndex<FlatpakModel>("flatpaks", x => x.Name, x => x.Categories);
            var available = await _unprivilegedOperationService.ListAppstreamFlatpak();

            var models = available.Select(u => new FlatpakModel
            {
                Name = u.Name,
                Version = u.Version,
                Summary = u.Summary,
                IconPath = $"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{u.Id}.png",
                Id = u.Id,
                Categories = u.Categories,
                Kind = u.Kind == 0
                    ? "App"
                    : "Runtime",
            }).ToList();
            await new DatabaseService().AddToDatabase(models.ToList(), "flatpaks");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to refresh installed packages: {e.Message}");
        }
    }
}